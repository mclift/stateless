using System;
using System.Threading.Tasks;
using Stateless.Reflection;
using Xunit;

namespace Stateless.Tests
{
    public class GuardConditionFixture
    {
        [Fact]
        public void SyncNoArgumentGuardIgnoresPackedArgsAndPreservesDescription()
        {
            var invocation = Invocation("CanMove", "custom guard");
            var calls = 0;
            var condition = new StateMachine<State, Trigger>.GuardCondition(() =>
            {
                calls++;
                return true;
            }, invocation);

            Assert.True(condition.Guard(new object[] { 1, "ignored" }));
            Assert.Equal(1, calls);
            Assert.Equal("custom guard", condition.Description);
            Assert.Same(invocation, condition.MethodDescription);
        }

        [Fact]
        public void SyncPackedGuardReceivesOriginalArgs()
        {
            var expectedArgs = new object[] { 1, "two" };
            object[] actualArgs = null;
            var condition = new StateMachine<State, Trigger>.GuardCondition(args =>
            {
                actualArgs = args;
                return args.Length == 2;
            }, Invocation("CanMove"));

            Assert.True(condition.Guard(expectedArgs));
            Assert.Same(expectedArgs, actualArgs);
        }

        [Fact]
        public void SyncGuardThrowsArgumentNullExceptionForNullGuard()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new StateMachine<State, Trigger>.GuardCondition((Func<object[], bool>)null, Invocation("CanMove")));

            Assert.Equal("guard", exception.ParamName);
        }

        [Fact]
        public void SyncGuardThrowsArgumentNullExceptionForNullDescription()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new StateMachine<State, Trigger>.GuardCondition(args => true, null));

            Assert.Equal("description", exception.ParamName);
        }

        [Fact]
        public async Task AsyncNoArgumentGuardIgnoresPackedArgsAndPreservesDescription()
        {
            var invocation = Invocation("CanMoveAsync", "custom async guard", InvocationInfo.Timing.Asynchronous);
            var calls = 0;
            var condition = new StateMachine<State, Trigger>.GuardConditionAsync(() =>
            {
                calls++;
                return Task.FromResult(true);
            }, invocation);

            Assert.True(await condition.GuardAsync(new object[] { 1, "ignored" }));
            Assert.Equal(1, calls);
            Assert.Equal("custom async guard", condition.Description);
            Assert.Same(invocation, condition.MethodDescription);
        }

        [Fact]
        public async Task AsyncPackedGuardReceivesOriginalArgs()
        {
            var expectedArgs = new object[] { 1, "two" };
            object[] actualArgs = null;
            var condition = new StateMachine<State, Trigger>.GuardConditionAsync(args =>
            {
                actualArgs = args;
                return Task.FromResult(args.Length == 2);
            }, Invocation("CanMoveAsync", timing: InvocationInfo.Timing.Asynchronous));

            Assert.True(await condition.GuardAsync(expectedArgs));
            Assert.Same(expectedArgs, actualArgs);
        }

        [Fact]
        public void AsyncGuardThrowsArgumentNullExceptionForNullGuard()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new StateMachine<State, Trigger>.GuardConditionAsync((Func<object[], Task<bool>>)null, Invocation("CanMoveAsync")));

            Assert.Equal("guard", exception.ParamName);
        }

        [Fact]
        public void AsyncGuardThrowsArgumentNullExceptionForNullDescription()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new StateMachine<State, Trigger>.GuardConditionAsync(args => Task.FromResult(true), null));

            Assert.Equal("description", exception.ParamName);
        }

        private static InvocationInfo Invocation(
            string methodName,
            string description = null,
            InvocationInfo.Timing timing = InvocationInfo.Timing.Synchronous)
        {
            return new InvocationInfo(methodName, description, timing);
        }
    }
}
