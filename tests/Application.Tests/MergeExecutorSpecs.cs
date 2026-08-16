using BitwardenSharp.Application.Abstractions;
using BitwardenSharp.Application.Duplicates;
using BitwardenSharp.Application.Merging;
using BitwardenSharp.Domain.Vault;
using NSubstitute;
using Shouldly;
using Xunit;

namespace BitwardenSharp.Application.Tests;

public class MergeExecutorSpecs
{
    private static (IVaultClient Vault, Dictionary<string, VaultItem> Store) FakeVault(
        params VaultItem[] items)
    {
        var store = items.ToDictionary(i => i.Id);
        var vault = Substitute.For<IVaultClient>();

        vault.GetItemAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(store[call.ArgAt<string>(0)]));

        vault.UpdateItemAsync(Arg.Any<VaultItem>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var item = call.ArgAt<VaultItem>(0);
                store[item.Id] = item;
                return Task.FromResult(item);
            });

        vault.DeleteItemAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(call => { store.Remove(call.ArgAt<string>(0)); return Task.CompletedTask; });

        return (vault, store);
    }

    [Fact]
    public async Task A_dry_run_reads_and_computes_but_writes_nothing()
    {
        var group = new DuplicateScanner().Scan([
            TestVault.Login("keep", uris: ["https://example.com/a"]),
            TestVault.Login("drop", uris: ["https://example.com/b"]),
        ]).Groups.Single();

        var (vault, store) = FakeVault([.. group.Members]);

        var outcome = await new MergeExecutor(vault).ApplyAsync(group, dryRun: true, TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(MergeStatus.Merged);
        outcome.Changes.ShouldContain(c => c.StartsWith("+uri"));
        outcome.DeletedItemIds.ShouldBeEmpty();
        store.Count.ShouldBe(2);
        await vault.DidNotReceive().UpdateItemAsync(Arg.Any<VaultItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Applying_unions_the_uris_onto_the_survivor_and_trashes_the_loser()
    {
        var group = new DuplicateScanner().Scan([
            TestVault.Login("keep", uris: ["https://example.com/a"]),
            TestVault.Login("drop", uris: ["https://example.com/b"]),
        ]).Groups.Single();

        var (vault, store) = FakeVault([.. group.Members]);

        var outcome = await new MergeExecutor(vault).ApplyAsync(group, dryRun: false, TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(MergeStatus.Merged);
        store.Count.ShouldBe(1);
        store[group.Survivor.Id].Uris.Select(u => u.Uri)
            .ShouldBe(["https://example.com/a", "https://example.com/b"], ignoreOrder: true);

        // Soft delete: the trash is the only undo a merge has.
        await vault.Received().DeleteItemAsync(
            group.Losers.Single().Id, permanent: false, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The core safety property: if the survivor does not read back with the merged content, no
    /// loser is deleted, so nothing is lost and the operation can simply be re-run.
    /// </summary>
    [Fact]
    public async Task A_survivor_that_does_not_verify_leaves_every_loser_in_place()
    {
        var group = new DuplicateScanner().Scan([
            TestVault.Login("keep", uris: ["https://example.com/a"]),
            TestVault.Login("drop", uris: ["https://example.com/b"]),
        ]).Groups.Single();

        var (vault, store) = FakeVault([.. group.Members]);

        // The vault silently accepts the write but stores the original — the failure mode a
        // blind "update then delete" would turn into data loss.
        vault.UpdateItemAsync(Arg.Any<VaultItem>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<VaultItem>(0)));

        var outcome = await new MergeExecutor(vault).ApplyAsync(group, dryRun: false, TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(MergeStatus.VerificationFailed);
        outcome.DeletedItemIds.ShouldBeEmpty();
        store.Count.ShouldBe(2);
        await vault.DidNotReceive().DeleteItemAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_review_only_group_is_refused_without_touching_the_vault()
    {
        var group = new DuplicateScanner().Scan([
            TestVault.Login("NUC-01", username: "root", uris: ["https://10.0.0.11:8006/"]),
            TestVault.Login("NUC-02", username: "root", uris: ["https://10.0.0.12:8006/"]),
        ]).Groups.Single();

        var (vault, store) = FakeVault([.. group.Members]);

        var outcome = await new MergeExecutor(vault).ApplyAsync(group, dryRun: false, TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(MergeStatus.Skipped);
        store.Count.ShouldBe(2);
        await vault.DidNotReceive().GetItemAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_failure_partway_through_reports_it_rather_than_continuing()
    {
        var group = new DuplicateScanner().Scan([
            TestVault.Login("keep", uris: ["https://example.com/a"]),
            TestVault.Login("drop", uris: ["https://example.com/b"]),
        ]).Groups.Single();

        var (vault, _) = FakeVault([.. group.Members]);
        vault.UpdateItemAsync(Arg.Any<VaultItem>(), Arg.Any<CancellationToken>())
            .Returns<VaultItem>(_ => throw new InvalidOperationException("vault rejected the edit"));

        var outcome = await new MergeExecutor(vault).ApplyAsync(group, dryRun: false, TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(MergeStatus.Failed);
        outcome.Message.ShouldNotBeNull().ShouldContain("vault rejected the edit");
        await vault.DidNotReceive().DeleteItemAsync(
            Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }
}
