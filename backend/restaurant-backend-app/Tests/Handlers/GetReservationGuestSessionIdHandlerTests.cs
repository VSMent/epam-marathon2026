using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers.Reservations;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace APIHandler.Tests.Handlers;

public class GetReservationGuestSessionIdHandlerTests
{
    private readonly Mock<IGuestSessionService> _mockService = new();
    private readonly Mock<ILambdaContext> _mockContext = new();
    private readonly Mock<ILambdaLogger> _mockLogger = new();
    private readonly GetReservationGuestSessionIdHandler _handler;

    public GetReservationGuestSessionIdHandlerTests()
    {
        _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
        _handler = new GetReservationGuestSessionIdHandler(_mockService.Object);
    }

    [Fact]
    public async Task HandleRequest_MissingReservationId_Returns400()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string>()
        };

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("reservationId", response.Body);
    }

    [Fact]
    public async Task HandleRequest_MissingAuth_Returns401()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "reservationId", "res-1" } },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext()
        };

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(401, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_ValidOwner_Returns200WithGuestSessionId()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "reservationId", "res-1" } },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { { "sub", "owner-user" } }
                }
            }
        };

        _mockService
            .Setup(s => s.GetOwnerGuestSessionIdAsync("owner-user", "res-1"))
            .ReturnsAsync("owner-session-123");

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("owner-session-123", response.Body);
    }

    [Fact]
    public async Task HandleRequest_NonOwner_Returns403()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "reservationId", "res-1" } },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { { "sub", "wrong-user" } }
                }
            }
        };

        _mockService
            .Setup(s => s.GetOwnerGuestSessionIdAsync("wrong-user", "res-1"))
            .ThrowsAsync(new ActionForbiddenException("Forbidden"));

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_ReservationNotFound_Returns404()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "reservationId", "res-unknown" } },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { { "sub", "owner-user" } }
                }
            }
        };

        _mockService
            .Setup(s => s.GetOwnerGuestSessionIdAsync("owner-user", "res-unknown"))
            .ThrowsAsync(new ReservationNotFoundException("Reservation not found."));

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(404, response.StatusCode);
    }
}

