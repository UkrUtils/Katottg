namespace UkrUtils.Katottg.Tests;

public class KatottgCategoryTests
{
    [Theory]
    [InlineData('O', KatottgCategory.Region)]
    [InlineData('О', KatottgCategory.Region)] // кирилична О
    [InlineData('K', KatottgCategory.SpecialStatusCity)]
    [InlineData('К', KatottgCategory.SpecialStatusCity)]
    [InlineData('P', KatottgCategory.District)]
    [InlineData('Р', KatottgCategory.District)]
    [InlineData('H', KatottgCategory.Community)]
    [InlineData('Н', KatottgCategory.Community)]
    [InlineData('M', KatottgCategory.City)]
    [InlineData('М', KatottgCategory.City)]
    [InlineData('T', KatottgCategory.UrbanTypeSettlement)]
    [InlineData('Т', KatottgCategory.UrbanTypeSettlement)]
    [InlineData('X', KatottgCategory.Settlement)]
    [InlineData('Х', KatottgCategory.Settlement)]
    [InlineData('C', KatottgCategory.Village)]
    [InlineData('С', KatottgCategory.Village)]
    [InlineData('B', KatottgCategory.CityDistrict)]
    [InlineData('В', KatottgCategory.CityDistrict)]
    [InlineData('c', KatottgCategory.Village)]
    [InlineData('с', KatottgCategory.Village)] // кирилична мала с
    [InlineData('Z', KatottgCategory.Unknown)]
    [InlineData(' ', KatottgCategory.Unknown)]
    public void FromLetter(char letter, KatottgCategory expected)
        => Assert.Equal(expected, KatottgCategoryExtensions.FromLetter(letter));

    [Fact]
    public void ToLetter_RoundTrips()
    {
        foreach (var c in Enum.GetValues<KatottgCategory>())
        {
            if (c == KatottgCategory.Unknown)
            {
                Assert.Equal('\0', c.ToLetter());
                continue;
            }
            Assert.Equal(c, KatottgCategoryExtensions.FromLetter(c.ToLetter()));
        }
    }

    [Theory]
    [InlineData(KatottgCategory.City, true)]
    [InlineData(KatottgCategory.SpecialStatusCity, true)]
    [InlineData(KatottgCategory.UrbanTypeSettlement, true)]
    [InlineData(KatottgCategory.Settlement, true)]
    [InlineData(KatottgCategory.Village, true)]
    [InlineData(KatottgCategory.Region, false)]
    [InlineData(KatottgCategory.District, false)]
    [InlineData(KatottgCategory.Community, false)]
    [InlineData(KatottgCategory.CityDistrict, false)]
    [InlineData(KatottgCategory.Unknown, false)]
    public void IsPopulatedPlace(KatottgCategory c, bool expected) => Assert.Equal(expected, c.IsPopulatedPlace());

    [Fact]
    public void Names_AreDefinedForAllCategories()
    {
        Assert.Equal("село", KatottgCategory.Village.ToUkrainianName());
        Assert.Equal("місто", KatottgCategory.City.ToUkrainianName());
        Assert.Equal("с.", KatottgCategory.Village.ToShortPrefix());
        Assert.Equal("м.", KatottgCategory.City.ToShortPrefix());
        Assert.Equal("м.", KatottgCategory.SpecialStatusCity.ToShortPrefix());
        Assert.Equal("смт", KatottgCategory.UrbanTypeSettlement.ToShortPrefix());
        Assert.Equal("с-ще", KatottgCategory.Settlement.ToShortPrefix());
        Assert.Equal("", KatottgCategory.District.ToShortPrefix());
        Assert.Equal("р-н", KatottgCategory.District.ToShortSuffix());
        Assert.Equal("обл.", KatottgCategory.Region.ToShortSuffix());
        Assert.Equal("громада", KatottgCategory.Community.ToShortSuffix());
        Assert.Equal("р-н", KatottgCategory.CityDistrict.ToShortSuffix());
        Assert.Equal("", KatottgCategory.Village.ToShortSuffix());

        foreach (var c in Enum.GetValues<KatottgCategory>())
            Assert.False(string.IsNullOrEmpty(c.ToUkrainianName()));
    }
}
