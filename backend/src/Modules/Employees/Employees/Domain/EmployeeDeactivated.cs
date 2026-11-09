using BuildingBlocks.Domain;

namespace Employees.Domain;

/// <summary>
/// Raised when an employee's status changes from Active to Inactive. No handler exists yet: it is the seam a future
/// module (leave, attendance, access revocation) subscribes to with an <c>IDomainEventHandler</c>, without the
/// Employees module knowing about it. Dispatched after the change is saved.
/// </summary>
public sealed record EmployeeDeactivated(Guid EmployeeId) : DomainEvent;
