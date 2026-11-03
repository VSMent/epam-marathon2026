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

public class GetGroupSessionsHandlerTests
{
    private readonly Mock<IGuestSessionService> _mockService = new();
    private readonly Mock<ILambdaContext> _mockContext = new();
    private readonly Mock<ILambdaLogger> _mockLogger = new();
    private readonly GetGroupSessionsHandler _handler;

    public GetGroupSessionsHandlerTests()
    {
        _mockContext.Setup(c => c.Logger).Returns(_mockLogger.Object);
        _handler = new GetGroupSessionsHandler(_mockService.Object);
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
    public async Task HandleRequest_MissingAuthorizerClaims_Returns401()
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
    public async Task HandleRequest_ValidRequest_Returns200WithData()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "groupReservationId", "grp-1" } },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { { "sub", "owner-user" } }
                }
            }
        };

        var expectedResponse = new GetAllSessionsResponse([], 0);
        _mockService
            .Setup(s => s.GetAllSessionsAsync("owner-user", "grp-1"))
            .ReturnsAsync(expectedResponse);

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(200, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_ForbiddenOwner_Returns403()
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
            .Setup(s => s.GetAllSessionsAsync("wrong-user", "grp-1"))
            .ThrowsAsync(new ActionForbiddenException("Forbidden"));

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task HandleRequest_GroupNotFound_Returns404()
    {
        var request = new APIGatewayProxyRequest
        {
            PathParameters = new Dictionary<string, string> { { "groupReservationId", "grp-1" } },
            RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
            {
                Authorizer = new APIGatewayCustomAuthorizerContext
                {
                    Claims = new Dictionary<string, string> { { "sub", "owner-user" } }
                }
            }
        };

        _mockService
            .Setup(s => s.GetAllSessionsAsync("owner-user", "grp-1"))
            .ThrowsAsync(new GroupReservationNotFoundException());

        var response = await _handler.HandleRequest(request, _mockContext.Object);

        Assert.Equal(404, response.StatusCode);
    }
}
