using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;

namespace Employees.Application;

/// <summary>Soft delete: the persistence layer turns the removal into a flag, and the email becomes reusable.</summary>
public sealed record DeleteEmployeeCommand(Guid Id) : ICommand<Result>;

internal sealed class DeleteEmployeeHandler(IEmployeeRepository employees, IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteEmployeeCommand, Result>
{
    public async ValueTask<Result> Handle(DeleteEmployeeCommand command, CancellationToken cancellationToken)
    {
        var employee = await employees.FindAsync(command.Id, cancellationToken);
        if (employee is null)
            return EmployeeErrors.NotFound;

        employees.Remove(employee);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
