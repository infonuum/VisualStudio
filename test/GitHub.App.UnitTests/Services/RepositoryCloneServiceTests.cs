﻿using System.Reactive.Linq;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using UnitTests;
using GitHub.Services;
using System.Linq.Expressions;
using System;
using GitHub.Models;
using GitHub.Api;

public class RepositoryCloneServiceTests
{
    public class TheCloneRepositoryMethod : TestBaseClass
    {
        [Test]
        public async Task ClonesToRepositoryPathAsync()
        {
            var serviceProvider = Substitutes.ServiceProvider;
            var operatingSystem = serviceProvider.GetOperatingSystem();
            var vsGitServices = serviceProvider.GetVSGitServices();
            var cloneService = serviceProvider.GetRepositoryCloneService();

            await cloneService.CloneRepository("https://github.com/foo/bar", @"c:\dev\bar");

            operatingSystem.Directory.Received().CreateDirectory(@"c:\dev\bar");
            await vsGitServices.Received().Clone("https://github.com/foo/bar", @"c:\dev\bar", true);
        }

        [TestCase("https://github.com/foo/bar", 1, nameof(UsageModel.MeasuresModel.NumberOfClones))]
        [TestCase("https://github.com/foo/bar", 0, nameof(UsageModel.MeasuresModel.NumberOfGitHubClones))]
        [TestCase("https://github.com/foo/bar", 0, nameof(UsageModel.MeasuresModel.NumberOfEnterpriseClones))]
        [TestCase("https://enterprise.com/foo/bar", 1, nameof(UsageModel.MeasuresModel.NumberOfClones))]
        [TestCase("https://enterprise.com/foo/bar", 0, nameof(UsageModel.MeasuresModel.NumberOfEnterpriseClones))]
        [TestCase("https://enterprise.com/foo/bar", 0, nameof(UsageModel.MeasuresModel.NumberOfGitHubClones))]
        public async Task UpdatesMetricsWhenRepositoryClonedAsync(string cloneUrl, int numberOfCalls, string counterName)
        {
            var serviceProvider = Substitutes.ServiceProvider;
            var operatingSystem = serviceProvider.GetOperatingSystem();
            var vsGitServices = serviceProvider.GetVSGitServices();
            var teamExplorerServices = Substitute.For<ITeamExplorerServices>();
            var graphqlFactory = Substitute.For<IGraphQLClientFactory>();
            var gitHubContextService = Substitute.For<IGitHubContextService>();
            var usageTracker = Substitute.For<IUsageTracker>();
            var cloneService = new RepositoryCloneService(operatingSystem, vsGitServices, teamExplorerServices,
                graphqlFactory, gitHubContextService, usageTracker);

            await cloneService.CloneRepository(cloneUrl, @"c:\dev\bar");
            var model = UsageModel.Create(Guid.NewGuid());

            await usageTracker.Received(numberOfCalls).IncrementCounter(
                Arg.Is<Expression<Func<UsageModel.MeasuresModel, int>>>(x =>
                    ((MemberExpression)x.Body).Member.Name == counterName));
        }

        [TestCase("https://github.com/foo/bar", false, 1, nameof(UsageModel.MeasuresModel.NumberOfClones))]
        [TestCase("https://github.com/foo/bar", false, 1, nameof(UsageModel.MeasuresModel.NumberOfGitHubClones))]
        [TestCase("https://github.com/foo/bar", false, 0, nameof(UsageModel.MeasuresModel.NumberOfEnterpriseClones))]
        [TestCase("https://enterprise.com/foo/bar", false, 1, nameof(UsageModel.MeasuresModel.NumberOfClones))]
        [TestCase("https://enterprise.com/foo/bar", false, 1, nameof(UsageModel.MeasuresModel.NumberOfEnterpriseClones))]
        [TestCase("https://enterprise.com/foo/bar", false, 0, nameof(UsageModel.MeasuresModel.NumberOfGitHubClones))]

        [TestCase("https://github.com/foo/bar", true, 1, nameof(UsageModel.MeasuresModel.NumberOfGitHubOpens))]
        [TestCase("https://github.com/foo/bar", true, 0, nameof(UsageModel.MeasuresModel.NumberOfEnterpriseOpens))]
        [TestCase("https://enterprise.com/foo/bar", true, 1, nameof(UsageModel.MeasuresModel.NumberOfEnterpriseOpens))]
        [TestCase("https://enterprise.com/foo/bar", true, 0, nameof(UsageModel.MeasuresModel.NumberOfGitHubOpens))]
        public async Task UpdatesMetricsWhenCloneOrOpenRepositoryAsync(string cloneUrl, bool dirExists, int numberOfCalls, string counterName)
        {
            var repositoryPath = @"c:\dev\bar";
            var cloneDialogResult = new CloneDialogResult(repositoryPath, cloneUrl);
            var serviceProvider = Substitutes.ServiceProvider;
            var operatingSystem = serviceProvider.GetOperatingSystem();
            operatingSystem.Directory.DirectoryExists(repositoryPath).Returns(dirExists);
            var vsGitServices = serviceProvider.GetVSGitServices();
            var teamExplorerServices = Substitute.For<ITeamExplorerServices>();
            var graphqlFactory = Substitute.For<IGraphQLClientFactory>();
            var gitHubContextService = Substitute.For<IGitHubContextService>();
            var usageTracker = Substitute.For<IUsageTracker>();
            var cloneService = new RepositoryCloneService(operatingSystem, vsGitServices, teamExplorerServices,
                graphqlFactory, gitHubContextService, usageTracker);

            await cloneService.CloneOrOpenRepository(cloneDialogResult);

            await usageTracker.Received(numberOfCalls).IncrementCounter(
                Arg.Is<Expression<Func<UsageModel.MeasuresModel, int>>>(x =>
                    ((MemberExpression)x.Body).Member.Name == counterName));
        }

        [TestCase(@"c:\default\repo", @"c:\default", 1, nameof(UsageModel.MeasuresModel.NumberOfClonesToDefaultClonePath))]
        [TestCase(@"c:\not_default\repo", @"c:\default", 0, nameof(UsageModel.MeasuresModel.NumberOfClonesToDefaultClonePath))]
        public async Task UpdatesMetricsWhenDefaultClonePath(string targetPath, string defaultPath, int numberOfCalls, string counterName)
        {
            var serviceProvider = Substitutes.ServiceProvider;
            var operatingSystem = serviceProvider.GetOperatingSystem();
            var vsGitServices = serviceProvider.GetVSGitServices();
            var teamExplorerServices = Substitute.For<ITeamExplorerServices>();
            vsGitServices.GetLocalClonePathFromGitProvider().Returns(defaultPath);
            var graphqlFactory = Substitute.For<IGraphQLClientFactory>();
            var gitHubContextService = Substitute.For<IGitHubContextService>();
            var usageTracker = Substitute.For<IUsageTracker>();
            var cloneService = new RepositoryCloneService(operatingSystem, vsGitServices, teamExplorerServices,
                graphqlFactory, gitHubContextService, usageTracker);

            await cloneService.CloneRepository("https://github.com/foo/bar", targetPath);
            var model = UsageModel.Create(Guid.NewGuid());

            await usageTracker.Received(numberOfCalls).IncrementCounter(
                Arg.Is<Expression<Func<UsageModel.MeasuresModel, int>>>(x =>
                    ((MemberExpression)x.Body).Member.Name == counterName));
        }
    }
}
