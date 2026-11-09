using System.Text.Json.Serialization;
using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;
using Employees.Domain;

namespace Employees.Application;

public sealed record UpdateEmployeeCommand : EmployeeInput, ICommand<Result<EmployeeDetails>>
{
    /// <summary>Taken from the route, never from the body.</summary>
    [JsonIgnore]
    public Guid Id { get; init; }
}

internal sealed class UpdateEmployeeValidator(IClock clock) : EmployeeInputValidator<UpdateEmployeeCommand>(clock);

internal sealed class UpdateEmployeeHandler(IEmployeeRepository employees, IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateEmployeeCommand, Result<EmployeeDetails>>
{
    public async ValueTask<Result<EmployeeDetails>> Handle(UpdateEmployeeCommand command, CancellationToken cancellationToken)
    {
        var employee = await employees.FindAsync(command.Id, cancellationToken);
        if (employee is null)
            return EmployeeErrors.NotFound;

        var data = command.ToData();
        var emailOwner = await employees.FindIdByEmailAsync(Employee.NormalizeEmail(data.Email), cancellationToken);
        if (emailOwner is not null && emailOwner != employee.Id)
            return EmployeeErrors.EmailInUse;

        employee.Update(data);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return employee.ToDetails();
    }
}
