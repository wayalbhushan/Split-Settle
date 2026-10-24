namespace SplitSettle.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SplitSettle.Api.DTOs;
using SplitSettle.Api.Services;

[ApiController]
public class BalancesAndSettlementsController : ControllerBase
{
    private readonly ISettlementService _settlementService;

    public BalancesAndSettlementsController(ISettlementService settlementService)
    {
        _settlementService = settlementService;
    }

    [HttpGet("api/groups/{groupId:int}/balances")]
    public async Task<ActionResult<IReadOnlyList<BalanceResponse>>> GetBalances(
        int groupId,
        CancellationToken cancellationToken)
    {
        var balances = await _settlementService.GetBalancesByGroupIdAsync(groupId, cancellationToken);
        return Ok(balances);
    }

    [HttpGet("api/groups/{groupId:int}/settlements")]
    public async Task<ActionResult<IReadOnlyList<SettlementResponse>>> GetSettlements(
        int groupId,
        CancellationToken cancellationToken)
    {
        var settlements = await _settlementService.GetSettlementsByGroupIdAsync(groupId, cancellationToken);
        return Ok(settlements);
    }
}
