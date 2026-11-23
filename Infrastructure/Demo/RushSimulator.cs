using System.Collections.Concurrent;
using Core.Enums;
using Core.Interfaces;
using Core.Models;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Demo;

public sealed class RushSimulator(
    IServiceScopeFactory scopes,
    IOptions<RushOptions> options,
    TimeProvider clock,
    ILogger<RushSimulator> logger) : IRushSimulator, IAsyncDisposable
{
    private readonly RushOptions _o = options.Value;
    private readonly ConcurrentDictionary<int, RushRun> _runs = new();
    private readonly ConcurrentDictionary<int, DateTime> _lastStarted = new();
    private readonly CancellationTokenSource _shutdown = new();

    public async Task<RushStartResult> StartAsync(int eventId, Guid startedBy, CancellationToken ct = default)
    {
        bool hasWaitingRoom;
        using (var scope = scopes.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SeatRushContext>();
            var ev = await db.Events
                .Where(e => e.Id == eventId)
                .Select(e => new { e.ShowTime, e.HasWaitingRoom })
                .FirstOrDefaultAsync(ct);

            if (ev is null)
                return new RushStartResult(RushStartOutcome.NotFound);
            if (ev.ShowTime <= clock.GetUtcNow().UtcDateTime)
                return new RushStartResult(RushStartOutcome.ShowingStarted);

            hasWaitingRoom = ev.HasWaitingRoom;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        if (_runs.ContainsKey(eventId))
            return new RushStartResult(RushStartOutcome.AlreadyRunning);
        if (_lastStarted.TryGetValue(eventId, out var last) && now - last < _o.Cooldown)
            return new RushStartResult(RushStartOutcome.CoolingDown);

        var mode = hasWaitingRoom ? RushMode.Extreme : RushMode.Normal;
        var run = new RushRun(eventId, mode, now, CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token));
        if (!_runs.TryAdd(eventId, run))
            return new RushStartResult(RushStartOutcome.AlreadyRunning);

        _lastStarted[eventId] = now;
        run.Cts.CancelAfter(_o.MaxDuration);

        long? starterPosition = null;
        try
        {
            if (mode == RushMode.Extreme)
                starterPosition = await FloodLineAsync(run, startedBy);
        }
        catch
        {
            await run.Cts.CancelAsync();
            _runs.TryRemove(eventId, out _);
            throw;
        }

        run.Main = Task.Run(() => RunAsync(run));
        return new RushStartResult(RushStartOutcome.Started, mode, starterPosition);
    }

    public async Task StopAsync(int eventId)
    {
        if (!_runs.TryGetValue(eventId, out var run))
            return;

        await run.Cts.CancelAsync();
        if (run.Main is not null)
            await run.Main;
        _runs.TryRemove(eventId, out _);
    }

    public RushStatus GetStatus(int eventId) =>
        _runs.TryGetValue(eventId, out var run)
            ? new RushStatus(true, run.Mode, run.Arrived, run.Active, run.SeatsBooked, run.StartedAt)
            : new RushStatus(false, null, 0, 0, 0, null);

    private async Task<long?> FloodLineAsync(RushRun run, Guid starter)
    {
        using var scope = scopes.CreateScope();
        var room = scope.ServiceProvider.GetRequiredService<IWaitingRoomService>();
        var holds = scope.ServiceProvider.GetRequiredService<ISeatHoldService>();
        var sessions = scope.ServiceProvider.GetRequiredService<IBookingSessionService>();

        foreach (var (seatId, hold) in await holds.GetActiveHoldsAsync(run.EventId))
            if (hold.HolderId == starter)
                await holds.ReleaseAsync(seatId, starter);
        await sessions.EndAsync(run.EventId, starter);
        await room.LeaveLineAsync(run.EventId, starter);

        var firstWave = Enumerable.Range(0, Math.Min(_o.StarterJoinsAfter, _o.ExtremeBots)).Select(_ => NewBotId()).ToList();
        await Task.WhenAll(firstWave.Select(bot => room.JoinAsync(run.EventId, bot)));
        foreach (var bot in firstWave)
            run.Track(Task.Run(() => VisitAsync(run, bot, alreadyInLine: true)));

        var status = await room.JoinAsync(run.EventId, starter);
        return status.Position;
    }

    private async Task RunAsync(RushRun run)
    {
        var ct = run.Cts.Token;
        try
        {
            if (run.Mode == RushMode.Extreme)
            {
                var rest = _o.ExtremeBots - Math.Min(_o.StarterJoinsAfter, _o.ExtremeBots);
                var gap = rest > 0 ? _o.ExtremeArrivalWindow / rest : TimeSpan.Zero;
                for (var i = 0; i < rest && !ct.IsCancellationRequested; i++)
                {
                    var bot = NewBotId();
                    run.Track(Task.Run(() => VisitAsync(run, bot, alreadyInLine: false)));
                    await Task.Delay(gap, ct);
                }
            }
            else
            {
                while (!ct.IsCancellationRequested && !run.SoldOut)
                {
                    var bot = NewBotId();
                    run.Track(Task.Run(() => VisitAsync(run, bot, alreadyInLine: false)));
                    await Task.Delay(Between(_o.NormalArrivalMin, _o.NormalArrivalMax), ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await run.WhenAllBotsDoneAsync();
            _runs.TryRemove(run.EventId, out _);
        }
    }

    private async Task VisitAsync(RushRun run, Guid bot, bool alreadyInLine)
    {
        var ct = run.Cts.Token;
        var eventId = run.EventId;
        var held = new List<int>();
        var inLine = alreadyInLine;
        run.Arrive();

        using var scope = scopes.CreateScope();
        var services = scope.ServiceProvider;
        var room = services.GetRequiredService<IWaitingRoomService>();
        var sessions = services.GetRequiredService<IBookingSessionService>();
        var holds = services.GetRequiredService<ISeatHoldService>();

        try
        {
            if (run.Mode == RushMode.Extreme)
            {
                if (!inLine)
                {
                    await room.JoinAsync(eventId, bot);
                    inLine = true;
                }

                while ((await room.GetStatusAsync(eventId, bot)).State != QueueState.Admitted)
                    await Task.Delay(Jitter(_o.PollInterval), ct);
                inLine = false;
            }

            var session = await sessions.GetOrStartAsync(eventId, bot);
            if (run.Mode == RushMode.Extreme)
                await room.EnterAsync(eventId, bot, session.ExpiresAt);

            await Task.Delay(Between(_o.LookMin, _o.LookMax), ct);

            for (var attempt = 0; attempt < 2 && held.Count == 0; attempt++)
            {
                var map = await services.GetRequiredService<ISeatService>().GetSeatMapAsync(eventId, null, ct);
                var group = PickGroup(map, Random.Shared.Next(1, run.Mode == RushMode.Extreme ? 5 : 4));
                if (group.Count == 0)
                {
                    run.SoldOut = true;
                    return;
                }

                foreach (var seatId in group)
                {
                    if ((await holds.HoldAsync(seatId, bot, ct)).Succeeded)
                        held.Add(seatId);
                    await Task.Delay(Between(_o.ClickGapMin, _o.ClickGapMax), ct);
                }
            }

            if (held.Count == 0)
                return;

            await Task.Delay(Between(_o.DecideMin, _o.DecideMax), ct);

            var bookChance = run.Mode == RushMode.Extreme ? _o.ExtremeBookChance : _o.NormalBookChance;
            if (Random.Shared.NextDouble() < bookChance)
            {
                var result = await services.GetRequiredService<IBookingService>().CheckoutAsync(
                    eventId, bot, held, PaymentMethod.PayAtCounter, Guid.NewGuid().ToString("N"), ct);

                if (result.Outcome == CheckoutOutcome.Booked)
                {
                    run.Booked(held.Count);
                    held.Clear();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Rush bot {Bot} failed on event {EventId}", bot, eventId);
        }
        finally
        {
            try
            {
                foreach (var seatId in held)
                    await holds.ReleaseAsync(seatId, bot);
                await sessions.EndAsync(eventId, bot);
                if (inLine)
                    await room.LeaveLineAsync(eventId, bot);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Rush bot {Bot} cleanup failed", bot);
            }

            run.Leave();
        }
    }

    private static List<int> PickGroup(SeatMapSnapshot map, int size)
    {
        var available = map.Seats.Where(s => s.Status == SeatStatus.Available).ToList();
        if (available.Count == 0)
            return [];

        var rows = map.Seats.Select(s => s.Row).Distinct().ToList();
        var rowIndex = rows.Select((r, i) => (r, i)).ToDictionary(x => x.r, x => x.i);
        var rowWidth = map.Seats.GroupBy(s => s.Row).ToDictionary(g => g.Key, g => g.Max(s => s.Number));
        var free = available.ToDictionary(s => (s.Row, s.Number), s => s.Id);

        double Weight(SeatSnapshot s)
        {
            var depth = rows.Count > 1 ? rowIndex[s.Row] / (double)(rows.Count - 1) : 0.5;
            var across = rowWidth[s.Row] > 1 ? (s.Number - 1) / (double)(rowWidth[s.Row] - 1) : 0.5;
            return 0.15 + Math.Exp(-Math.Pow((depth - 0.6) / 0.28, 2)) * Math.Exp(-Math.Pow((across - 0.5) / 0.25, 2));
        }

        var weights = available.Select(Weight).ToArray();
        var pick = Random.Shared.NextDouble() * weights.Sum();
        var anchor = available[^1];
        for (var i = 0; i < available.Count; i++)
        {
            pick -= weights[i];
            if (pick <= 0)
            {
                anchor = available[i];
                break;
            }
        }

        var group = new List<int> { anchor.Id };
        int left = anchor.Number - 1, right = anchor.Number + 1;
        while (group.Count < size)
        {
            if (free.TryGetValue((anchor.Row, right), out var r))
            {
                group.Add(r);
                right++;
            }
            else if (free.TryGetValue((anchor.Row, left), out var l))
            {
                group.Add(l);
                left--;
            }
            else
            {
                break;
            }
        }

        return group;
    }

    private static Guid NewBotId() => RushBots.Id(Random.Shared.NextInt64(1, 1L << 47));

    private static TimeSpan Between(TimeSpan min, TimeSpan max) =>
        min + (max - min) * Random.Shared.NextDouble();

    private static TimeSpan Jitter(TimeSpan t) => Between(t * 0.8, t * 1.2);

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync();
        await Task.WhenAll(_runs.Values.Select(r => r.Main ?? Task.CompletedTask));
        _shutdown.Dispose();
    }

    private sealed class RushRun(int eventId, RushMode mode, DateTime startedAt, CancellationTokenSource cts)
    {
        private readonly List<Task> _bots = [];
        private int _arrived, _active, _booked;

        public int EventId { get; } = eventId;
        public RushMode Mode { get; } = mode;
        public DateTime StartedAt { get; } = startedAt;
        public CancellationTokenSource Cts { get; } = cts;
        public Task? Main { get; set; }
        public volatile bool SoldOut;

        public int Arrived => Volatile.Read(ref _arrived);
        public int Active => Volatile.Read(ref _active);
        public int SeatsBooked => Volatile.Read(ref _booked);

        public void Arrive()
        {
            Interlocked.Increment(ref _arrived);
            Interlocked.Increment(ref _active);
        }

        public void Leave() => Interlocked.Decrement(ref _active);
        public void Booked(int seats) => Interlocked.Add(ref _booked, seats);

        public void Track(Task bot)
        {
            lock (_bots) _bots.Add(bot);
        }

        public Task WhenAllBotsDoneAsync()
        {
            lock (_bots) return Task.WhenAll(_bots.ToArray());
        }
    }
}
