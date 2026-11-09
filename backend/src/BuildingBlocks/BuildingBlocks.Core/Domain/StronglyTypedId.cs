namespace BuildingBlocks.Domain;

/// <summary>
/// Base for strongly-typed ids (e.g. <c>record ArticleId(Guid Value) : StronglyTypedId&lt;Guid&gt;(Value);</c>)
/// when you want to avoid primitive-obsession on identifiers. Reference entities may also use a bare Guid.
/// </summary>
public abstract record StronglyTypedId<TValue>(TValue Value)
    where TValue : notnull
{
    public sealed override string ToString() => Value.ToString() ?? string.Empty;
}
