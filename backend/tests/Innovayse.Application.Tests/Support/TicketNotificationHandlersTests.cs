namespace Innovayse.Application.Tests.Support;

using Innovayse.Application.Auth.Interfaces;
using Innovayse.Application.Notifications.Commands.SendEmail;
using Innovayse.Application.Support.Events;
using Innovayse.Domain.Clients;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Events;
using Innovayse.Domain.Support.Interfaces;
using Moq;
using Wolverine;
using Xunit;

/// <summary>
/// Unit tests for the Support module's notification handlers
/// (<see cref="TicketCreatedHandler"/>, <see cref="TicketRepliedHandler"/>, <see cref="TicketClosedHandler"/>).
/// </summary>
public sealed class TicketNotificationHandlersTests
{
    private static Client MakeClient(string userId = "user-1") =>
        Client.Create(userId, "Jane", "Doe", "jane@example.com");

    public sealed class TicketCreatedHandlerTests
    {
        [Fact]
        public async Task HandleAsync_WhenDepartmentHasEmail_SendsTicketCreatedEmailAsync()
        {
            var ticket = Ticket.Create(clientId: 1, "Help", "Something's broken", departmentId: 5, TicketPriority.Medium);
            var department = Department.Create("Support", "support@example.com");

            var ticketRepo = new Mock<ITicketRepository>();
            ticketRepo.Setup(r => r.FindByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

            var departmentRepo = new Mock<IDepartmentRepository>();
            departmentRepo.Setup(r => r.FindByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(department);

            var bus = new Mock<IMessageBus>();

            var handler = new TicketCreatedHandler(bus.Object, ticketRepo.Object, departmentRepo.Object);

            await handler.HandleAsync(new TicketCreatedEvent(ticket.Id, 1, 5), CancellationToken.None);

            bus.Verify(b => b.InvokeAsync(
                It.Is<SendEmailCommand>(c => c.To == "support@example.com" && c.TemplateSlug == "ticket-created"),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenDepartmentNotFound_DoesNotSendAsync()
        {
            var ticketRepo = new Mock<ITicketRepository>();
            var departmentRepo = new Mock<IDepartmentRepository>();
            departmentRepo.Setup(r => r.FindByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync((Department?)null);

            var bus = new Mock<IMessageBus>();
            var handler = new TicketCreatedHandler(bus.Object, ticketRepo.Object, departmentRepo.Object);

            await handler.HandleAsync(new TicketCreatedEvent(1, 1, 5), CancellationToken.None);

            bus.VerifyNoOtherCalls();
            ticketRepo.VerifyNoOtherCalls();
        }
    }

    public sealed class TicketRepliedHandlerTests
    {
        [Fact]
        public async Task HandleAsync_WhenStaffReplies_NotifiesClientAsync()
        {
            var ticket = Ticket.Create(1, "Help", "Something's broken", departmentId: 5, TicketPriority.Medium);
            var client = MakeClient();

            var ticketRepo = new Mock<ITicketRepository>();
            ticketRepo.Setup(r => r.FindByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

            var clientRepo = new Mock<IClientRepository>();
            clientRepo.Setup(r => r.FindByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(client);

            var userService = new Mock<IUserService>();
            userService.Setup(s => s.FindByIdAsync(client.UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((client.UserId, "jane@example.com"));

            var departmentRepo = new Mock<IDepartmentRepository>();
            var bus = new Mock<IMessageBus>();

            var handler = new TicketRepliedHandler(bus.Object, ticketRepo.Object, departmentRepo.Object, clientRepo.Object, userService.Object);

            await handler.HandleAsync(new TicketRepliedEvent(ticket.Id, IsStaffReply: true), CancellationToken.None);

            bus.Verify(b => b.InvokeAsync(
                It.Is<SendEmailCommand>(c => c.To == "jane@example.com" && c.TemplateSlug == "ticket-replied"),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);
            departmentRepo.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HandleAsync_WhenClientReplies_NotifiesDepartmentAsync()
        {
            var ticket = Ticket.Create(1, "Help", "Something's broken", departmentId: 5, TicketPriority.Medium);
            var department = Department.Create("Support", "support@example.com");

            var ticketRepo = new Mock<ITicketRepository>();
            ticketRepo.Setup(r => r.FindByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

            var departmentRepo = new Mock<IDepartmentRepository>();
            departmentRepo.Setup(r => r.FindByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(department);

            var clientRepo = new Mock<IClientRepository>();
            var userService = new Mock<IUserService>();
            var bus = new Mock<IMessageBus>();

            var handler = new TicketRepliedHandler(bus.Object, ticketRepo.Object, departmentRepo.Object, clientRepo.Object, userService.Object);

            await handler.HandleAsync(new TicketRepliedEvent(ticket.Id, IsStaffReply: false), CancellationToken.None);

            bus.Verify(b => b.InvokeAsync(
                It.Is<SendEmailCommand>(c => c.To == "support@example.com" && c.TemplateSlug == "ticket-replied"),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);
            clientRepo.VerifyNoOtherCalls();
        }
    }

    public sealed class TicketClosedHandlerTests
    {
        [Fact]
        public async Task HandleAsync_SendsTicketClosedEmailToClientAsync()
        {
            var ticket = Ticket.Create(1, "Help", "Something's broken", departmentId: 5, TicketPriority.Medium);
            var client = MakeClient();

            var ticketRepo = new Mock<ITicketRepository>();
            ticketRepo.Setup(r => r.FindByIdAsync(ticket.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ticket);

            var clientRepo = new Mock<IClientRepository>();
            clientRepo.Setup(r => r.FindByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(client);

            var userService = new Mock<IUserService>();
            userService.Setup(s => s.FindByIdAsync(client.UserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((client.UserId, "jane@example.com"));

            var bus = new Mock<IMessageBus>();
            var handler = new TicketClosedHandler(bus.Object, ticketRepo.Object, clientRepo.Object, userService.Object);

            await handler.HandleAsync(new TicketClosedEvent(ticket.Id, 1), CancellationToken.None);

            bus.Verify(b => b.InvokeAsync(
                It.Is<SendEmailCommand>(c => c.To == "jane@example.com" && c.TemplateSlug == "ticket-closed"),
                It.IsAny<CancellationToken>(),
                It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_WhenClientNotFound_DoesNotSendAsync()
        {
            var ticketRepo = new Mock<ITicketRepository>();
            var clientRepo = new Mock<IClientRepository>();
            clientRepo.Setup(r => r.FindByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync((Client?)null);

            var userService = new Mock<IUserService>();
            var bus = new Mock<IMessageBus>();
            var handler = new TicketClosedHandler(bus.Object, ticketRepo.Object, clientRepo.Object, userService.Object);

            await handler.HandleAsync(new TicketClosedEvent(1, 1), CancellationToken.None);

            bus.VerifyNoOtherCalls();
        }
    }
}
