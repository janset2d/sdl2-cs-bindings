namespace Build.Tests.Fixtures.Seeders;

/// <summary>
/// Composable fixture seeder contract. A seeder materializes a coherent, production-shaped
/// slice of repository state (harvest output, manifest config, vcpkg-installed layout, etc.)
/// on top of a <see cref="FakeRepoBuilder"/>. Seeders are first-class types that live under
/// <c>Fixtures/Seeders/</c>; tests compose them via <see cref="FakeRepoBuilder.Seed"/> to
/// assemble a test environment without duplicating fixture shape across tests.
/// </summary>
public interface IFixtureSeeder
{
    void Apply(FakeRepoBuilder builder);
}
