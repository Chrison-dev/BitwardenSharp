using BitwardenSharp.Application.Duplicates;
using BitwardenSharp.Application.Merging;
using BitwardenSharp.Domain.Vault;
using Shouldly;
using Xunit;

namespace BitwardenSharp.Application.Tests;

public class MergeDraftSpecs
{
    private static Domain.Duplicates.DuplicateGroup Pair(
        VaultItem a, VaultItem b, params VaultItem[] rest) =>
        new DuplicateScanner().Scan([a, b, .. rest]).Groups.Single();

    private static Domain.Duplicates.DuplicateGroup SameSite() => Pair(
        TestVault.Login("keep", uris: ["https://example.com/a"], notes: "kept"),
        TestVault.Login("drop", uris: ["https://example.com/b"]));

    /// <summary>
    /// The fast path and "open the editor, change nothing" must produce the same item, or the
    /// queue's preview would be lying about what approving does.
    /// </summary>
    [Fact]
    public void The_default_draft_reproduces_the_additive_merge_exactly()
    {
        var group = SameSite();

        var viaDraft = MergeBuilder.Build(MergeDraft.Default(group)).Merged;
        var viaAdditive = MergeBuilder.Build(group.Survivor, [.. group.Losers]).Merged;

        viaDraft.Name.ShouldBe(viaAdditive.Name);
        viaDraft.Login!.Password.ShouldBe(viaAdditive.Login!.Password);
        viaDraft.Uris.Select(u => u.Uri).ShouldBe(viaAdditive.Uris.Select(u => u.Uri), ignoreOrder: true);
        viaDraft.Notes.ShouldBe(viaAdditive.Notes);
        viaDraft.FolderId.ShouldBe(viaAdditive.FolderId);
    }

    [Fact]
    public void A_default_draft_reports_no_overwrites()
    {
        MergeDraft.Default(SameSite()).Overwrites.ShouldBeEmpty();
    }

    [Fact]
    public void Values_every_member_agrees_on_are_marked_unanimous()
    {
        var draft = MergeDraft.Default(SameSite());

        // Both members carry the same credentials — that is what made them duplicates.
        draft.Username.Origin.ShouldBe(ValueOrigin.Unanimous);
        draft.Password.Origin.ShouldBe(ValueOrigin.Unanimous);

        // The names differ, so the value had to be chosen.
        draft.Name.Origin.ShouldNotBe(ValueOrigin.Unanimous);
    }

    [Fact]
    public void Choosing_the_other_member_flips_which_items_are_deleted()
    {
        var group = SameSite();
        var other = group.Losers.First();

        var draft = MergeDraft.Default(group) with { Target = MergeTarget.Existing(other.Id) };

        draft.TargetItem!.Id.ShouldBe(other.Id);
        draft.Doomed.Select(d => d.Id).ShouldBe([group.Survivor.Id]);
    }

    [Fact]
    public void Targeting_a_new_item_dooms_every_member()
    {
        var group = SameSite();

        var draft = MergeDraft.Default(group) with { Target = MergeTarget.NewItem };

        draft.TargetItem.ShouldBeNull();
        draft.Doomed.Count().ShouldBe(group.Members.Count);
        draft.Overwrites.ShouldBeEmpty("nothing exists yet to overwrite");
    }

    /// <summary>
    /// The CLI cannot move an attachment between items, so resolving into a third item would
    /// delete the only copy of the file along with its item.
    /// </summary>
    [Fact]
    public void A_group_with_an_attachment_cannot_be_merged_into_a_new_item()
    {
        var group = Pair(
            TestVault.Login("plain", uris: ["https://example.com/a"]),
            TestVault.Login("with file", uris: ["https://example.com/b"],
                attachments: [new ItemAttachment { Id = "a1", FileName = "recovery.pdf" }]));

        var draft = MergeDraft.Default(group);

        draft.CanTargetNewItem.ShouldBeFalse();
        draft.NewItemBlockedReason.ShouldContain("attachment");
    }

    [Fact]
    public void A_group_without_attachments_may_be_merged_into_a_new_item()
    {
        MergeDraft.Default(SameSite()).CanTargetNewItem.ShouldBeTrue();
    }

    // ── the risky path: replacing a password ─────────────────────────────────────────────────

    private static Domain.Duplicates.DuplicateGroup Conflict() => Pair(
        TestVault.Login("stale", username: "u", password: "old-password",
            uris: ["https://example.com/"], revised: DateTimeOffset.UtcNow.AddYears(-1)),
        TestVault.Login("current", username: "u", password: "new-password",
            uris: ["https://example.com/"], revised: DateTimeOffset.UtcNow));

    [Fact]
    public void Replacing_the_password_is_reported_as_an_overwrite()
    {
        var group = Conflict();
        var target = group.Survivor;
        var other = group.Members.First(m => m.Id != target.Id);

        var draft = MergeDraft.Default(group) with
        {
            Password = Resolved<string?>.From(other.Login!.Password, other.Id),
        };

        draft.ReplacesPassword.ShouldBeTrue();
        draft.Overwrites.ShouldContain(o => o.Field == "Password");
    }

    /// <summary>
    /// Picking the wrong side of a credential conflict is the one way this tool can lose something
    /// irreplaceable. The displaced password goes into Bitwarden's own password history, giving a
    /// recovery path that does not depend on the 30-day trash window.
    /// </summary>
    [Fact]
    public void A_displaced_password_is_kept_in_password_history()
    {
        var group = Conflict();
        var target = group.Survivor;
        var other = group.Members.First(m => m.Id != target.Id);

        var draft = MergeDraft.Default(group) with
        {
            Password = Resolved<string?>.From(other.Login!.Password, other.Id),
        };

        var merged = MergeBuilder.Build(draft).Merged;

        merged.Login!.Password.ShouldBe(other.Login.Password);
        merged.PasswordHistory.ShouldContain(h => h.Password == target.Login!.Password);
    }

    [Fact]
    public void Password_history_is_not_touched_when_the_password_is_unchanged()
    {
        var merged = MergeBuilder.Build(MergeDraft.Default(SameSite())).Merged;

        merged.PasswordHistory.ShouldBeEmpty();
    }

    [Fact]
    public void Overwrites_never_expose_the_secret_itself()
    {
        var group = Conflict();
        var other = group.Members.First(m => m.Id != group.Survivor.Id);

        var draft = MergeDraft.Default(group) with
        {
            Password = Resolved<string?>.From(other.Login!.Password, other.Id),
        };

        var change = draft.Overwrites.Single(o => o.Field == "Password");

        // The confirmation shows that the password changes, never what it changes to.
        change.Before.ShouldNotBe("old-password");
        change.After.ShouldNotBe("new-password");
        change.Before.ShouldAllBe(c => c == '•');
    }

    [Fact]
    public void A_hand_typed_value_is_marked_as_edited()
    {
        var draft = MergeDraft.Default(SameSite()) with
        {
            Name = Resolved<string>.Edited("Something neither item had"),
        };

        draft.Name.Origin.ShouldBe(ValueOrigin.Edited);
        MergeBuilder.Build(draft).Merged.Name.ShouldBe("Something neither item had");
    }

    [Fact]
    public void A_five_member_group_dooms_the_four_that_are_not_the_target()
    {
        var group = Pair(
            TestVault.Login("a", uris: ["https://eu.example.com/"]),
            TestVault.Login("b", uris: ["https://us.example.com/"]),
            TestVault.Login("c", uris: ["https://kr.example.com/"]),
            TestVault.Login("d", uris: ["https://account.example.com/"]),
            TestVault.Login("e", uris: ["https://dev.example.com/"]));

        group.Members.Count.ShouldBe(5);

        var draft = MergeDraft.Default(group);
        draft.Doomed.Count().ShouldBe(4);

        // Every member's URI survives onto the one that is kept.
        MergeBuilder.Build(draft).Merged.Uris.Count.ShouldBe(5);
    }
}
