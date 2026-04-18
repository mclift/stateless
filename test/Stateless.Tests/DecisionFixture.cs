using Stateless.Graph;
using Stateless.Reflection;
using Xunit;

namespace Stateless.Tests
{
    public class DecisionFixture
    {
        [Fact]
        public void ConstructorUsesDecisionNumberForNodeNameAndPreservesMethodMetadata()
        {
            var method = new InvocationInfo(
                "ChooseDestination",
                "raw \"selector\"\n[criterion]",
                InvocationInfo.Timing.Asynchronous);

            var decision = new Decision(method, 12);

            Assert.Equal("Decision12", decision.NodeName);
            Assert.Null(decision.StateName);
            Assert.Same(method, decision.Method);
            Assert.Empty(decision.Arriving);
            Assert.Empty(decision.Leaving);
        }
    }
}
