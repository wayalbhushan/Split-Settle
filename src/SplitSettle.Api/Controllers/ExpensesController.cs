namespace SplitSettle.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SplitSettle.Api.DTOs;
using SplitSettle.Api.Services;

[ApiController]
[Route("api/groups/{groupId:int}/expenses")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;

    public ExpensesController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpenseResponse>>> GetExpenses(
        int groupId,
        CancellationToken cancellationToken)
    {
        var expenses = await _expenseService.GetExpensesByGroupIdAsync(groupId, cancellationToken);
        return Ok(expenses);
    }

    [HttpPost]
    public async Task<ActionResult<ExpenseResponse>> CreateExpense(
        int groupId,
        [FromBody] CreateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        var expense = await _expenseService.CreateExpenseAsync(groupId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, expense);
    }

    [HttpPut("{expenseId:int}")]
    public async Task<ActionResult<ExpenseResponse>> UpdateExpense(
        int groupId,
        int expenseId,
        [FromBody] UpdateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await _expenseService.UpdateExpenseAsync(groupId, expenseId, request, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{expenseId:int}")]
    public async Task<IActionResult> DeleteExpense(
        int groupId,
        int expenseId,
        CancellationToken cancellationToken)
    {
        await _expenseService.DeleteExpenseAsync(groupId, expenseId, cancellationToken);
        return NoContent();
    }
}
