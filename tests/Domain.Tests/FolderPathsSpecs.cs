using BitwardenSharp.Domain.Vault;
using Shouldly;
using Xunit;

namespace BitwardenSharp.Domain.Tests;

public class FolderPathsSpecs
{
    private static VaultFolder Folder(string id, string name) => new() { Id = id, Name = name };

    private static readonly VaultFolder[] Homelab =
    [
        Folder("f1", "Homelab"),
        Folder("f2", "Homelab/Proxmox"),
        Folder("f3", "Homelab/Synology"),
        Folder("f4", "Homelab/Arr-Stack"),
        Folder("f5", "Homelab2"),
        Folder("f6", "Finance"),
    ];

    [Theory]
    [InlineData("Homelab/Proxmox", "Homelab")]
    [InlineData("A/B/C", "A/B")]
    [InlineData("Homelab", null)]
    public void Parent_is_everything_above_the_last_segment(string name, string? expected) =>
        FolderPaths.Parent(name).ShouldBe(expected);

    [Fact]
    public void Names_are_normalised_by_trimming_and_dropping_empty_segments() =>
        FolderPaths.Normalise(" Homelab / / Proxmox ").ShouldBe("Homelab/Proxmox");

    /// <summary>
    /// Segment-wise comparison, not a prefix match. "Homelab2" begins with "Homelab" as a string
    /// but is a completely separate folder, and a naive StartsWith would drag it along on a rename.
    /// </summary>
    [Fact]
    public void Descendancy_compares_whole_segments_not_string_prefixes()
    {
        FolderPaths.IsDescendantOf("Homelab/Proxmox", "Homelab").ShouldBeTrue();
        FolderPaths.IsDescendantOf("Homelab2", "Homelab").ShouldBeFalse();
        FolderPaths.IsDescendantOf("Homelab2/Thing", "Homelab").ShouldBeFalse();
        FolderPaths.IsDescendantOf("Homelab", "Homelab").ShouldBeFalse();
    }

    /// <summary>
    /// The behaviour that makes folders usable at all: Bitwarden stores them flat, so renaming a
    /// parent has to rewrite every child's name or the children stay behind under the old path.
    /// </summary>
    [Fact]
    public void Renaming_a_folder_carries_its_whole_subtree()
    {
        var plan = FolderPaths.PlanRename(Homelab, "f1", "Lab");

        plan.IsValid.ShouldBeTrue();
        plan.Renames.Select(r => r.NewName).ShouldBe(
            ["Lab", "Lab/Proxmox", "Lab/Synology", "Lab/Arr-Stack"], ignoreOrder: true);

        // Homelab2 and Finance are untouched.
        plan.Renames.Select(r => r.FolderId).ShouldNotContain("f5");
        plan.Renames.Select(r => r.FolderId).ShouldNotContain("f6");
    }

    [Fact]
    public void Renaming_a_leaf_touches_only_that_folder()
    {
        var plan = FolderPaths.PlanRename(Homelab, "f2", "PVE");

        plan.Renames.ShouldHaveSingleItem().NewName.ShouldBe("Homelab/PVE");
    }

    [Fact]
    public void Descendants_are_renamed_deepest_first()
    {
        VaultFolder[] deep =
        [
            Folder("a", "A"),
            Folder("b", "A/B"),
            Folder("c", "A/B/C"),
        ];

        var plan = FolderPaths.PlanRename(deep, "a", "Z");

        // The root goes first, then children deepest-first, so no two folders ever share a name
        // at any point during the sequence of writes.
        var depths = plan.Renames.Skip(1).Select(r => FolderPaths.Segments(r.NewName).Count).ToList();
        depths.ShouldBe(depths.OrderByDescending(d => d));
    }

    [Fact]
    public void A_name_that_would_collide_is_refused()
    {
        var plan = FolderPaths.PlanRename(Homelab, "f2", "Synology");

        plan.IsValid.ShouldBeFalse();
        plan.Error!.Message.ShouldContain("already exists");
    }

    [Fact]
    public void A_slash_in_a_new_name_is_refused_as_a_rename()
    {
        // Otherwise "rename" would silently become "move", with different consequences.
        var plan = FolderPaths.PlanRename(Homelab, "f2", "Other/Thing");

        plan.IsValid.ShouldBeFalse();
        plan.Error!.Message.ShouldContain("Move the folder instead");
    }

    [Fact]
    public void An_empty_name_is_refused() =>
        FolderPaths.PlanRename(Homelab, "f2", "   ").IsValid.ShouldBeFalse();

    [Fact]
    public void Moving_a_folder_rebases_its_subtree_under_the_new_parent()
    {
        var plan = FolderPaths.PlanMove(Homelab, "f1", "Finance");

        plan.IsValid.ShouldBeTrue();
        plan.Renames.Select(r => r.NewName).ShouldBe(
            ["Finance/Homelab", "Finance/Homelab/Proxmox", "Finance/Homelab/Synology",
             "Finance/Homelab/Arr-Stack"],
            ignoreOrder: true);
    }

    [Fact]
    public void Moving_a_folder_to_the_root_strips_its_parent_path()
    {
        var plan = FolderPaths.PlanMove(Homelab, "f2", newParentPath: null);

        plan.Renames.ShouldHaveSingleItem().NewName.ShouldBe("Proxmox");
    }

    /// <summary>Dropping a folder into its own subtree would orphan everything below it.</summary>
    [Fact]
    public void A_folder_cannot_be_moved_inside_itself()
    {
        FolderPaths.PlanMove(Homelab, "f1", "Homelab/Proxmox").IsValid.ShouldBeFalse();
        FolderPaths.PlanMove(Homelab, "f1", "Homelab").IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Moving_a_folder_where_it_already_is_is_a_no_op()
    {
        var plan = FolderPaths.PlanMove(Homelab, "f2", "Homelab");

        plan.IsValid.ShouldBeTrue();
        plan.Renames.ShouldBeEmpty();
    }

    [Fact]
    public void Creating_composes_the_full_path_from_parent_and_leaf()
    {
        var plan = FolderPaths.PlanCreate(Homelab, "Homelab", "Unifi");

        plan.Renames.ShouldHaveSingleItem().NewName.ShouldBe("Homelab/Unifi");
    }

    [Fact]
    public void Creating_a_root_folder_uses_the_leaf_alone() =>
        FolderPaths.PlanCreate(Homelab, null, "Travel")
            .Renames.ShouldHaveSingleItem().NewName.ShouldBe("Travel");

    [Fact]
    public void Creating_a_duplicate_is_refused() =>
        FolderPaths.PlanCreate(Homelab, "Homelab", "Proxmox").IsValid.ShouldBeFalse();

    [Fact]
    public void An_operation_on_a_folder_that_no_longer_exists_is_refused() =>
        FolderPaths.PlanRename(Homelab, "gone", "Whatever").IsValid.ShouldBeFalse();
}
