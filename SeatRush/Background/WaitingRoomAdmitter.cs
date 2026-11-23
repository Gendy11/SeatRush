using Core.Interfaces;
using Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace SeatRush.Background;

public class WaitingRoomAdmitter(
    IWaitingRoomService waitingRoom,
    IOptions<WaitingRoomOptions> options,
    ILogger<WaitingRoomAdmitter> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.AdmitInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await waitingRoom.AdmitAllAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Waiting room admit tick failed.");
            }
        }
    }
}
