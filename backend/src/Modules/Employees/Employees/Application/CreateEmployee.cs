using BuildingBlocks.Abstractions;
using BuildingBlocks.Application;
using Employees.Domain;

namespace Employees.Application;

public sealed record CreateEmployeeCommand : EmployeeInput, ICommand<Result<EmployeeDetails>>;

internal sealed class CreateEmployeeValidator(IClock clock) : EmployeeInputValidator<CreateEmployeeCommand>(clock);

internal sealed class CreateEmployeeHandler(IEmployeeRepository employees, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateEmployeeCommand, Result<EmployeeDetails>>
{
    public async ValueTask<Result<EmployeeDetails>> Handle(CreateEmployeeCommand command, CancellationToken cancellationToken)
    {
        var data = command.ToData();
        if (await employees.FindIdByEmailAsync(Employee.NormalizeEmail(data.Email), cancellationToken) is not null)
            return EmployeeErrors.EmailInUse;

        var employee = Employee.Create(data);
        await employees.AddAsync(employee, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return employee.ToDetails();
    }
}
