using Microsoft.EntityFrameworkCore;
using Moq;
using Praktikaportaal.Core.DTOs;
using Praktikaportaal.Core.Entities;
using Praktikaportaal.Core.Enums;
using Praktikaportaal.Core.Exceptions;
using Praktikaportaal.Core.Interfaces.Repositories;
using Praktikaportaal.Core.Services;
using Praktikaportaal.Infrastructure.Persistence;
using Praktikaportaal.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Praktikaportaal.Tests;

public class NotificationServiceTests
{
    private static (PraktikaportaalDbContext Context, NotificationService Service) Create()
    {
        var context = new PraktikaportaalDbContext(new DbContextOptionsBuilder<PraktikaportaalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        return (context, new NotificationService(new NotificationRepository(context), new UserRepository(context), new UnitOfWork(context)));
    }

    private static async Task<User> AddUserAsync(PraktikaportaalDbContext context, string email, bool active = true)
    {
        var user = new User { Email = email, FirstName = "A", LastName = "B", Role = UserRole.Student, IsActive = active };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Create_SavesUnreadNotificationForUser()
    {
        var (context, service) = Create();
        var user = await AddUserAsync(context, "a@student.ee");

        var dto = await service.CreateAsync(new CreateNotificationDto { UserId = user.Id, Title = "  Digest ", Message = "3 matches" });

        Assert.Equal("Digest", dto.Title);
        Assert.False(dto.IsRead);
        Assert.Equal(1, await context.Notifications.CountAsync(n => n.UserId == user.Id));
    }

    [Fact]
    public async Task Create_UnknownOrInactiveUser_Throws()
    {
        var (context, service) = Create();
        var inactive = await AddUserAsync(context, "off@student.ee", active: false);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateAsync(new CreateNotificationDto { UserId = Guid.NewGuid(), Title = "t", Message = "m" }));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateNotificationDto { UserId = inactive.Id, Title = "t", Message = "m" }));
        Assert.Equal(0, await context.Notifications.CountAsync());
    }

    [Fact]
    public async Task GetMy_ReturnsOnlyOwn_NewestFirst_WithUnreadCount_AndPaging()
    {
        var (context, service) = Create();
        var me = await AddUserAsync(context, "me@student.ee");
        var other = await AddUserAsync(context, "other@student.ee");
        for (int i = 1; i <= 3; i++)
        {
            await service.CreateAsync(new CreateNotificationDto { UserId = me.Id, Title = $"N{i}", Message = "m" });
            await Task.Delay(5); // distinct CreatedAt
        }
        await service.CreateAsync(new CreateNotificationDto { UserId = other.Id, Title = "Not mine", Message = "m" });

        var page1 = await service.GetMyNotificationsAsync(me.Id, new NotificationQueryParameters { PageSize = 2 });

        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(3, page1.UnreadCount);
        Assert.Equal(["N3", "N2"], page1.Items.Select(n => n.Title));
        Assert.DoesNotContain(page1.Items, n => n.Title == "Not mine");
    }

    [Fact]
    public async Task MarkRead_OwnNotification_ReducesUnreadCount_AndUnreadOnlyFilterWorks()
    {
        var (context, service) = Create();
        var me = await AddUserAsync(context, "me@student.ee");
        var first = await service.CreateAsync(new CreateNotificationDto { UserId = me.Id, Title = "A", Message = "m" });
        await service.CreateAsync(new CreateNotificationDto { UserId = me.Id, Title = "B", Message = "m" });

        await service.MarkReadAsync(first.Id, me.Id);
        await service.MarkReadAsync(first.Id, me.Id); // idempotent

        var unread = await service.GetMyNotificationsAsync(me.Id, new NotificationQueryParameters { UnreadOnly = true });
        Assert.Equal(1, unread.UnreadCount);
        Assert.Equal(["B"], unread.Items.Select(n => n.Title));
    }

    [Fact]
    public async Task MarkRead_SomeoneElsesNotification_LooksLikeNotFound()
    {
        var (context, service) = Create();
        var owner = await AddUserAsync(context, "owner@student.ee");
        var intruder = await AddUserAsync(context, "intruder@student.ee");
        var n = await service.CreateAsync(new CreateNotificationDto { UserId = owner.Id, Title = "Private", Message = "m" });

        await Assert.ThrowsAsync<NotFoundException>(() => service.MarkReadAsync(n.Id, intruder.Id));

        Assert.False((await context.Notifications.FindAsync(n.Id))!.IsRead);
    }

    [Fact]
    public async Task MarkAllRead_MarksOnlyOwnNotifications()
    {
        var (context, service) = Create();
        var me = await AddUserAsync(context, "me@student.ee");
        var other = await AddUserAsync(context, "other@student.ee");
        await service.CreateAsync(new CreateNotificationDto { UserId = me.Id, Title = "1", Message = "m" });
        await service.CreateAsync(new CreateNotificationDto { UserId = me.Id, Title = "2", Message = "m" });
        await service.CreateAsync(new CreateNotificationDto { UserId = other.Id, Title = "3", Message = "m" });

        int changed = await service.MarkAllReadAsync(me.Id);

        Assert.Equal(2, changed);
        Assert.Equal(0, (await service.GetMyNotificationsAsync(me.Id, new NotificationQueryParameters())).UnreadCount);
        Assert.Equal(1, (await service.GetMyNotificationsAsync(other.Id, new NotificationQueryParameters())).UnreadCount);
    }

    [Fact]
    public async Task MarkAllRead_WithNothingUnread_DoesNotSave()
    {
        var notifications = new Mock<INotificationRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        notifications.Setup(r => r.MarkAllReadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var changed = await new NotificationService(notifications.Object, Mock.Of<IUserRepository>(), unitOfWork.Object)
            .MarkAllReadAsync(Guid.NewGuid());

        Assert.Equal(0, changed);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

/// <summary>Notifications created as a side effect of application events.</summary>
public class ApplicationNotificationTests
{
    private static ApplicationService CreateService(PraktikaportaalDbContext context) => new(
        new ApplicationRepository(context), new InternshipRepository(context), new StudentProfileRepository(context),
        new NotificationRepository(context), new UnitOfWork(context));

    private static async Task<(PraktikaportaalDbContext Context, StudentProfile Student, CompanyProfile Company, Internship Internship)> SeedAsync()
    {
        var context = new PraktikaportaalDbContext(new DbContextOptionsBuilder<PraktikaportaalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var studentUser = new User { Email = "s@student.ee", FirstName = "Mari", LastName = "Tamm", Role = UserRole.Student };
        var student = new StudentProfile { User = studentUser, UserId = studentUser.Id, SchoolName = "S", StudyProgram = "P", Course = 1 };
        var companyUser = new User { Email = "c@test.ee", FirstName = "Jaan", LastName = "Kask", Role = UserRole.Company };
        var company = new CompanyProfile { User = companyUser, UserId = companyUser.Id, CompanyName = "Acme OÜ", RegistrationCode = "1" };
        var internship = new Internship { CompanyProfile = company, Title = "Backend Intern", Description = "d", Field = "IT", Status = InternshipStatus.Published };

        context.AddRange(student, company, internship);
        await context.SaveChangesAsync();
        return (context, student, company, internship);
    }

    [Fact]
    public async Task Apply_NotifiesTheCompanyUser()
    {
        var (context, student, company, internship) = await SeedAsync();

        await CreateService(context).ApplyAsync(student.Id, internship.Id, new CreateApplicationDto());

        var notification = await context.Notifications.SingleAsync();
        Assert.Equal(company.UserId, notification.UserId);
        Assert.Contains("Mari Tamm", notification.Message);
        Assert.Contains("Backend Intern", notification.Message);
    }

    [Fact]
    public async Task StatusChange_NotifiesTheStudentUser()
    {
        var (context, student, company, internship) = await SeedAsync();
        var service = CreateService(context);
        var applied = await service.ApplyAsync(student.Id, internship.Id, new CreateApplicationDto());

        await service.UpdateApplicationStatusAsync(applied.Id, company.Id, ApplicationStatus.Viewed);

        var forStudent = await context.Notifications.SingleAsync(n => n.UserId == student.UserId);
        Assert.Contains("Viewed", forStudent.Message);
        Assert.Contains("Acme OÜ", forStudent.Message);
    }

    [Fact]
    public async Task RejectedStatusChange_DoesNotNotifyOrChange()
    {
        var (context, student, company, internship) = await SeedAsync();
        var service = CreateService(context);
        var applied = await service.ApplyAsync(student.Id, internship.Id, new CreateApplicationDto());

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateApplicationStatusAsync(applied.Id, company.Id, ApplicationStatus.Accepted)); // not allowed from Submitted

        Assert.Equal(0, await context.Notifications.CountAsync(n => n.UserId == student.UserId));
    }
}
