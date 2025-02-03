using InfiniTicTacToe.Server.Models;
using InfiniTicTacToe.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace InfiniTicTacToe.Server.Controllers;

[ApiController]
[Route("api/game")]
public class GameController(GameStorage gameStorage, ILogger<GameController> logger) : ControllerBase
{
    [HttpGet("status")]
    public ActionResult<GameStatistics> GetCurrentStatus()
    {
        var status = gameStorage.GetStats();
        logger.LogInformation("Current status: {Games} games in progress", status.Games);
        return status;
    }
}
