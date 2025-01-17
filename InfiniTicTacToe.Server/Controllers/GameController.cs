using InfiniTicTacToe.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace InfiniTicTacToe.Server.Controllers;

[ApiController]
[Route("api/game")]
public class GameController(GameService gameService, ILogger<GameController> logger) : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<GameStatistics> GetCurrentStatus()
    {
        var status = gameService.GetStats();
        logger.LogInformation("Current status: {Games} games in progress", status.Games);
        return status;
    }
}
