using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.Robots.Dtos;
using Syntwin.Application.Robots.Interfaces;

namespace Syntwin.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/robot-models")]
public sealed class RobotModelsController : ControllerBase
{
    private readonly IRobotModelService _robotModelService;

    public RobotModelsController(IRobotModelService robotModelService)
    {
        _robotModelService = robotModelService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RobotModelResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<RobotModelResponse>>> List(
        CancellationToken cancellationToken)
    {
        var models = await _robotModelService.ListActiveAsync(cancellationToken);
        return Ok(models);
    }
}