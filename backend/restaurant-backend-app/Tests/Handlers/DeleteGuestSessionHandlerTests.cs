using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace APIHandler.Tests.Handlers;

public class DeleteGuestSessionHandlerTests
{
    private readonly Mock<IGuestSessionService> _mockService = new();
    private readonly Mock<ILambdaContext> _mockContext = new();
    private readonly Mock<ILambdaLogger> _mockLogger = new();
    private readonly DeleteGuestSessionHandler _handler;

    public DeleteGuestSessionHandlerTests()
    {
        _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
        _handler = new DeleteGuestSessionHandler(_mockService.Object);
    }

    [Fact]
    public async Task HandleRequest_MissingGroupReservationId_Returns400()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "guestSessionId", "session-1" } }
        };

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("groupReservationId", response.Body);
    }

    [Fact]
    public async Task HandleRequest_MissingGuestSessionId_Returns400()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "groupReservationId", "grp-1" } }
        };

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("guestSessionId", response.Body);
    }

    [Fact]
    public async Task HandleRequest_MissingAuth_Returns401()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "groupReservationId", "grp-1" },
                { "guestSessionId", "session-1" }
            },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext()
        };

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_ValidOwner_Returns200WithUpdatedSnapshot()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "groupReservationId", "grp-1" },
                { "guestSessionId", "session-1" }
            },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { { "sub", "owner-user" } }
                }
            }
        };

        var expectedSnapshot = new GetAllSessionsResponse([], 0);
        _mockService
            .Setup(s => s.DeleteGuestSessionAsync("owner-user", "grp-1", "session-1"))
            .ReturnsAsync(expectedSnapshot);

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(200, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_NonOwner_Returns403()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "groupReservationId", "grp-1" },
                { "guestSessionId", "session-1" }
            },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { { "sub", "wrong-user" } }
                }
            }
        };

        _mockService
            .Setup(s => s.DeleteGuestSessionAsync("wrong-user", "grp-1", "session-1"))
            .ThrowsAsync(new ActionForbiddenException("Forbidden"));

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_SessionNotFound_Returns404()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>
            {
                { "groupReservationId", "grp-1" },
                { "guestSessionId", "session-unknown" }
            },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { { "sub", "owner-user" } }
                }
            }
        };

        _mockService
            .Setup(s => s.DeleteGuestSessionAsync("owner-user", "grp-1", "session-unknown"))
            .ThrowsAsync(new GuestSessionNotFoundException("Not found"));

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(404, response.StatusCode);
    }
}

