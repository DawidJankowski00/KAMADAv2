using Kamada.Modules.Catalog.Domain.Categories;

namespace Kamada.Modules.Catalog.UnitTests;

public sealed class CategoryTreeTests
{
    // food ─┬─ drinks ── coffee
    //       └─ snacks
    // tools
    private static readonly Guid Food = Guid.NewGuid();
    private static readonly Guid Drinks = Guid.NewGuid();
    private static readonly Guid Coffee = Guid.NewGuid();
    private static readonly Guid Snacks = Guid.NewGuid();
    private static readonly Guid Tools = Guid.NewGuid();

    private static readonly CategoryTree Tree = new(
    [
        (Food, null),
        (Drinks, Food),
        (Coffee, Drinks),
        (Snacks, Food),
        (Tools, null),
    ]);

    [Fact]
    public void SelfAndDescendants_ReturnsWholeSubtree()
    {
        Tree.SelfAndDescendants(Food).ShouldBe([Food, Drinks, Coffee, Snacks], ignoreOrder: true);
        Tree.SelfAndDescendants(Coffee).ShouldBe([Coffee]);
    }

    [Fact]
    public void SelfAndDescendants_UnknownCategory_ReturnsEmpty()
    {
        Tree.SelfAndDescendants(Guid.NewGuid()).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(CycleCases))]
    public void WouldCreateCycle_DetectsMovesUnderItselfOrDescendant(Guid id, Guid? newParent, bool expected)
    {
        Tree.WouldCreateCycle(id, newParent).ShouldBe(expected);
    }

    public static TheoryData<Guid, Guid?, bool> CycleCases => new()
    {
        { Food, Food, true },
        { Food, Coffee, true },
        { Drinks, Coffee, true },
        { Coffee, Tools, false },
        { Drinks, null, false },
        { Snacks, Drinks, false },
    };

    [Fact]
    public void HasChildren_ReflectsHierarchy()
    {
        Tree.HasChildren(Food).ShouldBeTrue();
        Tree.HasChildren(Coffee).ShouldBeFalse();
    }
}
