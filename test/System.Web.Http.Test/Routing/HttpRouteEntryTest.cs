﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Http;
using System.Web.Http.Routing.Constraints;
using Microsoft.TestCommon;

namespace System.Web.Http.Routing
{
    public class HttpRouteEntryTest
    {
        [Theory]
        [InlineData(1, 2, 1, 2, -1)]
        [InlineData(1, 2, 2, 1, -1)]
        [InlineData(2, 1, 1, 2, 1)]
        [InlineData(2, 1, 2, 1, 1)]
        [InlineData(0, 0, 1, 2, -1)]
        [InlineData(0, 0, 2, 1, 1)]
        [InlineData(0, 0, Int32.MinValue, Int32.MaxValue, -1)]
        [InlineData(0, 0, Int32.MaxValue, Int32.MinValue, 1)]
        [InlineData(Int32.MinValue, Int32.MaxValue, 0, 0, -1)]
        [InlineData(Int32.MaxValue, Int32.MinValue, 0, 0, 1)]
        public void CompareTo_RespectsOrder(int prefixOrder1, int prefixOrder2, int order1, int order2, int expectedValue)
        {
            HttpRouteEntry x = new HttpRouteEntry() { PrefixOrder = prefixOrder1, Order = order1 };
            HttpRouteEntry y = new HttpRouteEntry() { PrefixOrder = prefixOrder2, Order = order2 };

            Assert.Equal(expectedValue, x.CompareTo(y));
        }

        [Fact]
        public void CompareTo_Returns0_ForEquivalentRoutes()
        {
            HttpRouteEntry x = CreateRouteEntry("Employees/{id}");
            HttpRouteEntry y = CreateRouteEntry("Employees/{id}");

            Assert.Equal(0, x.CompareTo(y));
        }

        [Theory]
        [InlineData("abc", "def")]
        [InlineData("abc", "a{x}")]
        [InlineData("abc", "{x}c")]
        [InlineData("abc", "{x:int}")]
        [InlineData("abc", "{x}")]
        [InlineData("abc", "{*x}")]
        [InlineData("{x:alpha}", "{x:int}")]
        [InlineData("{x:int}", "{x}")]
        [InlineData("{x:int}", "{*x}")]
        [InlineData("a{x}", "{x}")]
        [InlineData("{x}c", "{x}")]
        [InlineData("a{x}", "{*x}")]
        [InlineData("{x}c", "{*x}")]
        [InlineData("{x}", "{*x}")]
        [InlineData("{*x:maxlength(10)}", "{*x}")]
        [InlineData("abc/def", "abc/{x:int}")]
        [InlineData("abc/def", "abc/{x}")]
        [InlineData("abc/def", "abc/{*x}")]
        [InlineData("abc/{x:int}", "abc/{x}")]
        [InlineData("abc/{x:int}", "abc/{*x}")]
        [InlineData("abc/{x}", "abc/{*x}")]
        public void CompareTo_ComparesCorrectly(string earlier, string later)
        {
            HttpRouteEntry x = CreateRouteEntry(earlier);
            HttpRouteEntry y = CreateRouteEntry(later);

            Assert.True(x.CompareTo(y) < 0);
            Assert.True(y.CompareTo(x) > 0);
        }
        
        private static HttpRouteEntry CreateRouteEntry(string routeTemplate)
        {
            IHttpRoute route = new HttpRouteBuilder().BuildHttpRoute(routeTemplate, new HttpMethod[] { HttpMethod.Get }, "Controller", "Action");
            return new HttpRouteEntry() { Route = route, RouteTemplate = routeTemplate };
        }
    }
}