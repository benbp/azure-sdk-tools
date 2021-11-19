using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;

namespace Stress.Generator.Tests
{
    public class MainTests
    {
        [Fact]
        public void TestMain()
        {
            Program.Main(new string[]{"-h"});
        }
    }
}
