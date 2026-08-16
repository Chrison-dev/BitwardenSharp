using BitwardenSharp.Application.Duplicates;
using BitwardenSharp.Domain.Duplicates;
using BitwardenSharp.Domain.Vault;
using Shouldly;
using Xunit;

namespace BitwardenSharp.Application.Tests;

public class DuplicateScannerSpecs
{
    private readonly DuplicateScanner _scanner = new();

    [Fact]
    public void Subdomains_of_one_site_with_one_credential_are_an_exact_duplicate()
    {
        var result = _scanner.Scan([
            TestVault.Login("eu.battle.net", uris: ["https://eu.battle.net/login"]),
            TestVault.Login("us.battle.net", uris: ["https://us.battle.net/login"]),
            TestVault.Login("account.battle.net", uris: ["https://account.battle.net/"]),
        ]);

        var group = result.Groups.ShouldHaveSingleItem();
        group.Category.ShouldBe(DuplicateCategory.ExactDuplicate);
        group.Members.Count.ShouldBe(3);
        group.CanMerge.ShouldBeTrue();
        group.Losers.Count().ShouldBe(2);
    }

    [Fact]
    public void One_brand_under_two_tlds_is_a_related_domain_merge()
    {
        var result = _scanner.Scan([
            TestVault.Login("digikey.com", uris: ["https://auth.digikey.com/"]),
            TestVault.Login("digikey.co.nz", uris: ["https://www.digikey.co.nz/"]),
        ]);

        var group = result.Groups.ShouldHaveSingleItem();
        group.Category.ShouldBe(DuplicateCategory.RelatedDomain);
        group.CanMerge.ShouldBeTrue();
    }

    [Fact]
    public void Two_front_doors_onto_one_service_family_are_a_related_domain_merge()
    {
        var result = _scanner.Scan([
            TestVault.Login("Gmail", uris: ["https://mail.google.com/"]),
            TestVault.Login("YouTube", uris: ["https://www.youtube.com/"]),
        ]);

        var group = result.Groups.ShouldHaveSingleItem();
        group.Category.ShouldBe(DuplicateCategory.RelatedDomain);
    }

    /// <summary>
    /// Regression. Password reuse is not evidence of a duplicate: on a real vault a single
    /// password covered hundreds of unrelated accounts. Grouping on credentials alone would have
    /// proposed deleting live accounts.
    /// </summary>
    [Fact]
    public void One_password_reused_across_unrelated_sites_is_never_grouped()
    {
        var result = _scanner.Scan([
            TestVault.Login("Geekzone", uris: ["https://www.geekzone.co.nz/"]),
            TestVault.Login("Docker Hub", uris: ["https://hub.docker.com/"]),
            TestVault.Login("MyAnimeList", uris: ["https://myanimelist.net/"]),
        ]);

        result.Groups.ShouldBeEmpty();
    }

    /// <summary>
    /// Regression for the specific defect: the original rule accepted a group when <i>any one</i>
    /// of its domains belonged to a known service family, so a single Nintendo entry dragged nine
    /// unrelated sites in with it.
    /// </summary>
    [Fact]
    public void A_single_known_brand_among_unrelated_sites_does_not_make_them_related()
    {
        var result = _scanner.Scan([
            TestVault.Login("Nintendo", uris: ["https://accounts.nintendo.com/"]),
            TestVault.Login("Geekzone", uris: ["https://www.geekzone.co.nz/"]),
            TestVault.Login("EDDB", uris: ["https://eddb.io/"]),
        ]);

        result.Groups.ShouldNotContain(g => g.Category == DuplicateCategory.RelatedDomain);
    }

    /// <summary>
    /// Regression. A homelab reuses one login across many machines. Those are separate hosts, and
    /// merging them would delete the record of every host but one.
    /// </summary>
    [Fact]
    public void One_credential_across_distinct_hosts_is_review_only_and_never_merged()
    {
        var result = _scanner.Scan([
            TestVault.Login("NUC-01", username: "root", uris: ["https://10.0.0.11:8006/"]),
            TestVault.Login("NUC-02", username: "root", uris: ["https://10.0.0.12:8006/"]),
            TestVault.Login("NUC-03", username: "root", uris: ["https://10.0.0.13:8006/"]),
        ]);

        var group = result.Groups.ShouldHaveSingleItem();
        group.Category.ShouldBe(DuplicateCategory.InfrastructureSharedCredential);
        group.CanMerge.ShouldBeFalse();
        result.MergeableDeletions.ShouldBe(0);
    }

    [Fact]
    public void Same_site_and_username_with_different_passwords_is_a_conflict()
    {
        var result = _scanner.Scan([
            TestVault.Login("reddit A", username: "chrison", password: "old", uris: ["https://reddit.com/"]),
            TestVault.Login("reddit B", username: "chrison", password: "new", uris: ["https://www.reddit.com/"]),
        ]);

        var group = result.Groups.ShouldHaveSingleItem();
        group.Category.ShouldBe(DuplicateCategory.CredentialConflict);
        group.CanMerge.ShouldBeFalse();
    }

    [Fact]
    public void Native_app_uris_are_not_folded_into_web_domains()
    {
        // "com.google.android.gm" must not be reduced to a domain and matched against google.com.
        var result = _scanner.Scan([
            TestVault.Login("Gmail web", uris: ["https://mail.google.com/"]),
            TestVault.Login("Gmail app", uris: ["androidapp://com.google.android.gm"]),
        ]);

        result.Groups.ShouldNotContain(g => g.Category == DuplicateCategory.ExactDuplicate);
    }

    [Fact]
    public void The_richest_item_survives()
    {
        var sparse = TestVault.Login("sparse", uris: ["https://example.com/"]);
        var rich = TestVault.Login("rich", uris: ["https://example.com/"], totp: "SEED",
            notes: "recovery codes", folderId: "folder-1");

        var group = _scanner.Scan([sparse, rich]).Groups.ShouldHaveSingleItem();

        group.Survivor.Name.ShouldBe("rich");
    }

    [Fact]
    public void An_attachment_anywhere_in_the_group_blocks_the_merge()
    {
        var result = _scanner.Scan([
            TestVault.Login("plain", uris: ["https://example.com/"]),
            TestVault.Login("with file", uris: ["https://example.com/"],
                attachments: [new ItemAttachment { Id = "a1", FileName = "recovery.pdf" }]),
        ]);

        var group = result.Groups.ShouldHaveSingleItem();
        group.Category.ShouldBe(DuplicateCategory.ExactDuplicate);
        group.CanMerge.ShouldBeFalse();
        group.Warnings.ShouldContain(w => w.Code == "attachments" && w.IsBlocking);
    }

    [Fact]
    public void Two_different_totp_seeds_block_the_merge()
    {
        var result = _scanner.Scan([
            TestVault.Login("a", uris: ["https://example.com/"], totp: "SEED-ONE"),
            TestVault.Login("b", uris: ["https://example.com/"], totp: "SEED-TWO"),
        ]);

        var group = result.Groups.ShouldHaveSingleItem();
        group.CanMerge.ShouldBeFalse();
        group.Warnings.ShouldContain(w => w.Code == "totp-conflict" && w.IsBlocking);
    }

    [Fact]
    public void Usernames_differing_only_in_case_are_the_same_account()
    {
        var result = _scanner.Scan([
            TestVault.Login("a", username: "Chrison", uris: ["https://example.com/"]),
            TestVault.Login("b", username: "chrison", uris: ["https://example.com/"]),
        ]);

        result.Groups.ShouldHaveSingleItem().Category.ShouldBe(DuplicateCategory.ExactDuplicate);
    }

    [Fact]
    public void Items_without_a_password_are_not_grouped_as_duplicates()
    {
        var result = _scanner.Scan([
            TestVault.Login("a", password: null, uris: ["https://example.com/"]),
            TestVault.Login("b", password: null, uris: ["https://example.com/"]),
        ]);

        result.Groups.ShouldNotContain(g => g.Category == DuplicateCategory.ExactDuplicate);
    }

    [Fact]
    public void Group_ids_are_stable_across_repeated_scans_of_the_same_input()
    {
        VaultItem[] items = [
            TestVault.Login("a", uris: ["https://example.com/"]),
            TestVault.Login("b", uris: ["https://example.com/"]),
            TestVault.Login("c", username: "other", uris: ["https://other.com/"]),
            TestVault.Login("d", username: "other", uris: ["https://other.com/"]),
        ];

        var first = _scanner.Scan(items).Groups.Select(g => $"{g.Id}:{g.Survivor.Id}");
        var second = _scanner.Scan([.. items.Reverse()]).Groups.Select(g => $"{g.Id}:{g.Survivor.Id}");

        second.ShouldBe(first, ignoreOrder: true);
    }
}
