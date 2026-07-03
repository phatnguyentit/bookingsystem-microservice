using BookingSystem.SearchService.Api.Features.SearchCatalogs;
using BookingSystem.SearchService.Infrastructure.Search;
using FluentAssertions;
using NSubstitute;

namespace SearchService.Tests.Features;

public class SearchCatalogsHandlerTests
{
    private readonly ISearchService _searchService = Substitute.For<ISearchService>();

    [Fact]
    public async Task Handle_ForwardsAllQueryParametersVerbatim()
    {
        var expected = new SearchResult([], 0, 2, 10);
        _searchService.SearchCatalogsAsync(
                "beach", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), 200m, 2, 10,
                Arg.Any<CancellationToken>())
            .Returns(expected);
        var query = new SearchCatalogsQuery(
            "beach", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5), 200m, Page: 2, PageSize: 10);

        var result = await new SearchCatalogsHandler(_searchService).Handle(query, default);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Handle_DefaultsToFirstPageOfTwenty()
    {
        var query = new SearchCatalogsQuery(null, null, null, null);

        await new SearchCatalogsHandler(_searchService).Handle(query, default);

        await _searchService.Received(1).SearchCatalogsAsync(
            null, null, null, null, 1, 20, Arg.Any<CancellationToken>());
    }
}
