using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GroupReservations;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace APIHandler.Tests.Handlers;

public class SubmitGroupReservationHandlerTests
{
    private readonly Mock<IGuestSessionService> _mockService = new();
    private readonly Mock<ILambdaContext> _mockContext = new();
    private readonly Mock<ILambdaLogger> _mockLogger = new();
    private readonly SubmitGroupReservationHandler _handler;

    public SubmitGroupReservationHandlerTests()
    {
        _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
        _handler = new SubmitGroupReservationHandler(_mockService.Object);
    }

    [Fact]
    public async Task HandleRequest_MissingGroupReservationId_Returns400()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>()
        };

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("groupReservationId", response.Body);
    }

    [Fact]
    public async Task HandleRequest_MissingAuth_Returns401()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "groupReservationId", "grp-1" } },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext()
        };

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_ValidOwnerAndBody_Returns200()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "groupReservationId", "grp-1" } },
            Body = "{\"reservationId\": \"res-1\"}",
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { { "sub", "owner-user" } }
                }
            }
        };

        _mockService
            .Setup(s => s.SubmitGroupPreOrderAsync("owner-user", "grp-1", "res-1", _mockLogger.Object))
            .Returns(Task.CompletedTask);

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("Group reservation pre-order submitted successfully.", response.Body);
    }

    [Fact]
    public async Task HandleRequest_NonOwner_Returns403()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "groupReservationId", "grp-1" } },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { { "sub", "wrong-user" } }
                }
            }
        };

        _mockService
            .Setup(s => s.SubmitGroupPreOrderAsync("wrong-user", "grp-1", null, _mockLogger.Object))
            .ThrowsAsync(new ActionForbiddenException("Forbidden"));

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_GroupNotFound_Returns404()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "groupReservationId", "grp-unknown" } },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { { "sub", "owner-user" } }
                }
            }
        };

        _mockService
            .Setup(s => s.SubmitGroupPreOrderAsync("owner-user", "grp-unknown", null, _mockLogger.Object))
            .ThrowsAsync(new GroupReservationNotFoundException());

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(404, response.StatusCode);
    }
}

