using System.Globalization;
using Xunit;

namespace Stateless.Tests
{
    public class ParameterConversionResourcesFixture
    {
        [Fact]
        public void ResourceManager_IsCached()
        {
            var first = ParameterConversionResources.ResourceManager;
            var second = ParameterConversionResources.ResourceManager;

            Assert.Same(first, second);
        }

        [Fact]
        public void ResourceStrings_ReturnInvariantMessages()
        {
            var previousCulture = ParameterConversionResources.Culture;

            try
            {
                ParameterConversionResources.Culture = CultureInfo.InvariantCulture;

                Assert.Equal("An argument of type {0} is required in position {1}.", ParameterConversionResources.ArgOfTypeRequiredInPosition);
                Assert.Equal("Too many parameters have been supplied. Expecting {0} but got {1}.", ParameterConversionResources.TooManyParameters);
                Assert.Equal("The argument in position {0} is of type {1} but must be of type {2}.", ParameterConversionResources.WrongArgType);
            }
            finally
            {
                ParameterConversionResources.Culture = previousCulture;
            }
        }
    }
}
