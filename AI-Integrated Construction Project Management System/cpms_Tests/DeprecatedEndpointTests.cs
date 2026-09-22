using AutoMapper;
using cpms_Application.MyMapper;
using cpms_Application.Request.AiChat;
using cpms_Application.Request.Chat;
using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Request.SupplierRecommendation;
using cpms_Application.Request.User;
using cpms_Application.Request.UserAccount;
using cpms_Application.Request.Warehouse;
using cpms_Application.Response;
using cpms_Application.Services;
using cpms_API.Controllers;
using cpms_Domain;
using cpms_Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace cpms_Tests;

public class DeprecatedEndpointTests
{
    [Fact]
    public void PurchaseOrderWritesAreGone()
    {
        var controller = new PurchaseOrdersController(null!);

        Assert.Equal(410, StatusOf(controller.CreatePurchaseOrder(new CreatePurchaseOrderRequest())));
        Assert.Equal(410, StatusOf(controller.Approve(1, null)));
        Assert.Equal(410, StatusOf(controller.Reject(1, null)));
        Assert.Equal(410, StatusOf(controller.CreateFromShortages(new CreatePurchaseOrderRequest())));
        Assert.Equal(410, StatusOf(controller.Receive(1, new ReceivePurchaseOrderRequest())));
        Assert.Equal(410, StatusOf(controller.Ship(1, null)));
        Assert.Equal(410, StatusOf(controller.MarkProcessing(1, null)));
        Assert.Equal(410, StatusOf(controller.Cancel(1, null)));
    }

    [Fact]
    public void SelfRegistrationAndVerificationAreGone()
    {
        var controller = new AuthController(null!);

        Assert.Equal(410, StatusOf(controller.Register(new UserRegisterRequest())));
        Assert.Equal(410, StatusOf(controller.Verification(new VerificationEmailRequest())));
        Assert.Equal(410, StatusOf(controller.ResendVerification(new ResendVerificationRequest())));
    }

    [Fact]
    public void SupplierRecommendationsAreGone()
    {
        var controller = new SuppliersController(null!, null!);

        var result = controller.RecommendBalancedSuppliers(new BalancedSupplierRecommendationRequest());

        Assert.Equal(410, StatusOf(result));
    }

    [Fact]
    public void ChatEndpointsAreGone()
    {
        var controller = new ChatController(null!);

        Assert.Equal(410, StatusOf(controller.CreateConversation(new CreateConversationRequest())));
        Assert.Equal(410, StatusOf(controller.GetProjectConversations(1)));
        Assert.Equal(410, StatusOf(controller.GetMessages(1)));
        Assert.Equal(410, StatusOf(controller.SendMessage(1, new SendMessageRequest())));
        Assert.Equal(410, StatusOf(controller.UpdateMessage(1, new UpdateMessageRequest())));
        Assert.Equal(410, StatusOf(controller.DeleteMessage(1)));
        Assert.Equal(410, StatusOf(controller.MarkRead(1)));
    }

    [Fact]
    public void AiChatEndpointsAreGone()
    {
        var controller = new AiChatController(null!);

        Assert.Equal(410, StatusOf(controller.CreateSession(new CreateAiChatSessionRequest())));
        Assert.Equal(410, StatusOf(controller.GetSessions()));
        Assert.Equal(410, StatusOf(controller.GetMessages(1)));
        Assert.Equal(410, StatusOf(controller.SendMessage(1, new SendAiChatMessageRequest())));
        Assert.Equal(410, StatusOf(controller.DeleteSession(1)));
    }

    [Fact]
    public async Task SupplierRoleCannotBeAssigned()
    {
        var uow = new TestUnitOfWork();
        var account = new UserAccount
        {
            Id = 20,
            Email = "user@example.com",
            IsEmailVerified = true,
            Role = Role.CUSTOMER
        };
        uow.UserAccountRecords.Add(account);

        var response = await new UserAccountService(uow, CreateMapper(), new FakeClaimService(1, Role.ADMIN))
            .UpdateUserRoleProfileAsync(account.Id, new UpdateUserRoleRequest { Role = Role.SUPPLIER });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(Role.CUSTOMER, account.Role);
    }

    private static int? StatusOf(IActionResult result) =>
        Assert.IsType<ObjectResult>(result).StatusCode;

    private static IMapper CreateMapper() => new MapperConfiguration(configuration =>
        configuration.AddProfile<MapperConfigurationsProfile>(), NullLoggerFactory.Instance).CreateMapper();
}
