using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Services;
using Xunit;

namespace Praktikaportaal.Tests;

/// <summary>Pure unit tests of the allowed application status transitions.</summary>
public class ApplicationStatusRulesTests
{
    [Theory]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Viewed)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Viewed, ApplicationStatus.Interview)]
    [InlineData(ApplicationStatus.Viewed, ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Interview, ApplicationStatus.Accepted)]
    [InlineData(ApplicationStatus.Interview, ApplicationStatus.Rejected)]
    public void Company_AllowedTransitions(ApplicationStatus from, ApplicationStatus to) =>
        Assert.True(ApplicationStatusRules.CanCompanyChange(from, to));

    [Theory]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Accepted)]   // must be interviewed first
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Interview)]  // must be viewed first
    [InlineData(ApplicationStatus.Viewed, ApplicationStatus.Accepted)]
    [InlineData(ApplicationStatus.Interview, ApplicationStatus.Viewed)]     // no going back
    [InlineData(ApplicationStatus.Accepted, ApplicationStatus.Rejected)]    // final
    [InlineData(ApplicationStatus.Rejected, ApplicationStatus.Viewed)]      // final
    [InlineData(ApplicationStatus.Viewed, ApplicationStatus.Withdrawn)]     // student-only
    [InlineData(ApplicationStatus.Viewed, ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Viewed, ApplicationStatus.Viewed)]        // same status
    public void Company_ForbiddenTransitions(ApplicationStatus from, ApplicationStatus to) =>
        Assert.False(ApplicationStatusRules.CanCompanyChange(from, to));

    [Theory]
    [InlineData(ApplicationStatus.Submitted, true)]
    [InlineData(ApplicationStatus.Viewed, true)]
    [InlineData(ApplicationStatus.Interview, true)]
    [InlineData(ApplicationStatus.Accepted, false)]
    [InlineData(ApplicationStatus.Rejected, false)]
    [InlineData(ApplicationStatus.Withdrawn, false)]
    public void Student_CanWithdrawOnlyOpenApplications(ApplicationStatus status, bool expected) =>
        Assert.Equal(expected, ApplicationStatusRules.CanStudentWithdraw(status));

    [Fact]
    public void FinalStatuses_HaveNoNextStatuses()
    {
        Assert.Empty(ApplicationStatusRules.AllowedForCompany(ApplicationStatus.Accepted));
        Assert.Empty(ApplicationStatusRules.AllowedForCompany(ApplicationStatus.Rejected));
        Assert.Empty(ApplicationStatusRules.AllowedForCompany(ApplicationStatus.Withdrawn));
    }
}
