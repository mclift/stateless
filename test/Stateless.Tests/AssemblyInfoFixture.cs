using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Stateless.Tests
{
    public class AssemblyInfoFixture
    {
        [Fact]
        public void AssemblyVersion_RemainsFixedForFourXSeries()
        {
            var version = typeof(StateMachine<,>).Assembly.GetName().Version;

            Assert.Equal(new Version(4, 0, 0, 0), version);
        }

        [Fact]
        public void Assembly_IsClsCompliant()
        {
            var attribute = typeof(StateMachine<,>)
                .Assembly
                .GetCustomAttributes<CLSCompliantAttribute>()
                .Single();

            Assert.True(attribute.IsCompliant);
        }

        [Fact]
        public void InternalsVisibleTo_IsStrongNamedForTestAssembly()
        {
            var attribute = typeof(StateMachine<,>)
                .Assembly
                .GetCustomAttributes<InternalsVisibleToAttribute>()
                .Single(a => a.AssemblyName.StartsWith("Stateless.Tests,", StringComparison.Ordinal));

            Assert.Contains("PublicKey=", attribute.AssemblyName);
            Assert.DoesNotContain("PublicKey=null", attribute.AssemblyName);
        }
    }
}
