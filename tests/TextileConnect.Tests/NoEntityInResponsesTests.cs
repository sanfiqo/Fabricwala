using System.Reflection;
using System.Reflection.Emit;
using TextileConnect.Api;

namespace TextileConnect.Tests;

/// <summary>
/// Architecture rule: an endpoint never hands an EF entity (a class from TextileConnect.Domain) to the caller. Entities hold columns that are not meant for the screen
/// (keys, flags, internal notes) and tie the API shape to the database. Endpoints return a response record instead.
///
/// How it works: the compiled code of the API project is read instruction by instruction. Every call of a generic Results.* method (Results.Ok(value), Results.Created(uri, value) ...)
/// shows which type the value has, and every endpoint lambda shows its return type. The type, what it holds and what those hold (lists, paged wrappers, anonymous objects,
/// response records) must not contain a Domain class. Enums of the Domain (statuses, types) are fine.
/// </summary>
public sealed class NoEntityInResponsesTests
{
    private static readonly Assembly Api = typeof(CatalogueReader).Assembly;
    private static readonly Assembly Domain = typeof(TextileConnect.Domain.Orders.Order).Assembly;
    private static readonly Assembly Application = typeof(TextileConnect.Application.Common.PagedResponse<>).Assembly;

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => o.Value);

    private const BindingFlags AllMethods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    /// <summary>The first Domain class found inside the type, or null.</summary>
    private static Type? FindEntity(Type type, HashSet<Type> seen)
    {
        if (type.HasElementType) return FindEntity(type.GetElementType()!, seen);
        if (type.IsGenericParameter || !seen.Add(type)) return null;
        if (type.Assembly == Domain) return type.IsEnum ? null : type;
        if (type.IsGenericType)
            foreach (var argument in type.GetGenericArguments())
                if (FindEntity(argument, seen) is { } inner) return inner;
        // Our own records and anonymous objects: look at what they hold.
        var ours = type.Assembly == Api || type.Assembly == Application || type.Name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal);
        if (!ours) return null;
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (FindEntity(property.PropertyType, seen) is { } held) return held;
        return null;
    }

    private static IEnumerable<MethodInfo> CalledMethods(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        if (il is null) yield break;
        var typeArguments = method.DeclaringType is { IsGenericType: true } ? method.DeclaringType.GetGenericArguments() : null;
        var methodArguments = method.IsGenericMethod ? method.GetGenericArguments() : null;
        for (var i = 0; i < il.Length;)
        {
            var code = il[i++];
            var opcode = code == 0xFE ? OpCodesByValue[(short)(0xFE00 | il[i++])] : OpCodesByValue[code];
            int size;
            switch (opcode.OperandType)
            {
                case OperandType.InlineNone: size = 0; break;
                case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: size = 1; break;
                case OperandType.InlineVar: size = 2; break;
                case OperandType.InlineI8: case OperandType.InlineR: size = 8; break;
                case OperandType.InlineSwitch: size = 4 + 4 * BitConverter.ToInt32(il, i); break;
                default: size = 4; break;
            }
            if (opcode.OperandType == OperandType.InlineMethod)
            {
                MethodBase? target = null;
                try { target = method.Module.ResolveMethod(BitConverter.ToInt32(il, i), typeArguments, methodArguments); } catch (ArgumentException) { }
                if (target is MethodInfo called) yield return called;
            }
            i += size;
        }
    }

    /// <summary>
    /// An endpoint handler is a lambda that is given a service or the request. (A lambda that turns one object into another, like <c>x => new Notification { ... }</c> inside a Select,
    /// is not an endpoint and may build entities.)
    /// </summary>
    private static bool IsEndpointHandler(MethodInfo lambda)
        => lambda.Name.Contains("b__", StringComparison.Ordinal) && lambda.GetParameters().Any(p =>
            p.ParameterType == typeof(CancellationToken) || p.ParameterType == typeof(TextileConnect.Infrastructure.Persistence.MarketplaceDbContext)
            || p.ParameterType == typeof(System.Security.Claims.ClaimsPrincipal) || p.ParameterType == typeof(Microsoft.AspNetCore.Http.HttpContext)
            || p.ParameterType.IsInterface && p.ParameterType.Namespace?.StartsWith("TextileConnect", StringComparison.Ordinal) == true);

    /// <summary>Every place in the API project that returns a Domain class, as readable text.</summary>
    private static List<string> Violations()
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var type in Api.GetTypes())
        {
            foreach (var method in type.GetMethods(AllMethods).Cast<MethodBase>().Concat(type.GetConstructors(AllMethods)))
            {
                var host = type.DeclaringType ?? type;

                // 1. Results.Ok(value), Results.Created(uri, value), TypedResults.Json(value) ...
                foreach (var called in CalledMethods(method))
                    if (called.IsGenericMethod && called.DeclaringType is { Namespace: "Microsoft.AspNetCore.Http" } owner && owner.Name is "Results" or "TypedResults")
                        foreach (var argument in called.GetGenericArguments())
                            if (FindEntity(argument, []) is { } entity)
                                found.Add($"{host.Name}.{method.Name}: {called.Name}<{argument.Name}> returns the entity {entity.Name}");

                // 2. an endpoint lambda that returns a value directly
                if (method is MethodInfo lambda && IsEndpointHandler(lambda) && !type.Name.Contains("d__", StringComparison.Ordinal))
                {
                    var returned = lambda.ReturnType.IsGenericType && lambda.ReturnType.GetGenericTypeDefinition() == typeof(Task<>) ? lambda.ReturnType.GetGenericArguments()[0] : lambda.ReturnType;
                    if (FindEntity(returned, []) is { } entity)
                        found.Add($"{host.Name}.{method.Name}: an endpoint returns {returned.Name}, which holds the entity {entity.Name}");
                }
            }
        }
        return found.ToList();
    }

    [Fact]
    public void No_endpoint_returns_an_entity_from_the_domain()
        => Assert.True(Violations().Count == 0, "These endpoints return Domain classes. Return a response record instead:\n  " + string.Join("\n  ", Violations()));

    [Fact]
    public void The_scan_really_sees_the_results_calls_so_the_rule_cannot_pass_by_seeing_nothing()
    {
        var resultsCalls = Api.GetTypes().SelectMany(t => t.GetMethods(AllMethods).Cast<MethodBase>().Concat(t.GetConstructors(AllMethods)))
            .SelectMany(CalledMethods).Count(m => m.IsGenericMethod && m.DeclaringType is { Namespace: "Microsoft.AspNetCore.Http" } owner && owner.Name is "Results" or "TypedResults");
        Assert.True(resultsCalls > 50, $"Only {resultsCalls} generic Results calls were found: the scan no longer reads the endpoint code.");
    }

    [Fact]
    public void The_rule_itself_works_it_sees_entities_inside_lists_wrappers_and_anonymous_objects()
    {
        var order = typeof(TextileConnect.Domain.Orders.Order);
        Assert.Equal(order, FindEntity(order, []));
        Assert.Equal(order, FindEntity(typeof(List<TextileConnect.Domain.Orders.Order>), []));
        Assert.Equal(order, FindEntity(typeof(TextileConnect.Domain.Orders.Order[]), []));
        Assert.Equal(order, FindEntity(typeof(TextileConnect.Application.Common.PagedResponse<TextileConnect.Domain.Orders.Order>), []));
        Assert.Equal(order, FindEntity(new { Inner = new { Order = (TextileConnect.Domain.Orders.Order?)null } }.GetType(), []));
        Assert.Null(FindEntity(typeof(OrderResponse), []));                      // a response record
        Assert.Null(FindEntity(typeof(TextileConnect.Domain.Catalogue.ProductUnit), [])); // enums are fine
        Assert.Null(FindEntity(typeof(Dictionary<string, int>), []));
    }
}
