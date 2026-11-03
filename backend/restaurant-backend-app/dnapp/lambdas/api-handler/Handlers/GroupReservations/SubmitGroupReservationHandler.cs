using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Helpers;
using APIHandler.Models.GroupReservation;
using APIHandler.Services.GuestSessions;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GroupReservations;

public class SubmitGroupReservationHandler : IApiRequestHandler
{
    private readonly IGuestSessionService _guestSessionService;

    public SubmitGroupReservationHandler(IGuestSessionService guestSessionService)
    {
        _guestSessionService = guestSessionService ?? throw new ArgumentNullException(nameof(guestSessionService));
    }

    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        if (!request.TryGetPathValue("groupReservationId", out var groupReservationId))
        {
            return ResponseBuilder.CreateErrorResponse(400, "Missing 'groupReservationId' in path parameters.", context.Logger);
        }

        if (request.RequestContext?.Authorizer?.Claims == null ||
            !request.RequestContext.Authorizer.Claims.TryGetValue("sub", out var userId))
        {
            return ResponseBuilder.CreateErrorResponse(401, "Unauthorized.", context.Logger);
        }

        string? reservationId = null;
        if (!string.IsNullOrWhiteSpace(request.Body))
        {
            try
            {
                var body = JsonSerializer.Deserialize<SubmitGroupPreOrderRequest>(request.Body);
                reservationId = body?.ReservationId;
            }
            catch (JsonException)
            {
                // Optional body parsing error; fallback to null
            }
        }

        try
        {
            await _guestSessionService.SubmitGroupPreOrderAsync(userId, groupReservationId, reservationId, context.Logger);
            return ResponseBuilder.CreateSuccessResponse(new { message = "Group reservation pre-order submitted successfully." });
        }
        catch (GroupReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger);
        }
        catch (GroupReservationSubmittedException ex)
        {
            return ResponseBuilder.CreateErrorResponse(409, ex.Message, context.Logger);
        }
        catch (GroupReservationExpiredException ex)
        {
            return ResponseBuilder.CreateErrorResponse(409, ex.Message, context.Logger);
        }
        catch (ReservationNotFoundException ex)
        {
            return ResponseBuilder.CreateErrorResponse(404, ex.Message, context.Logger);
        }
        catch (ActionForbiddenException ex)
        {
            return ResponseBuilder.CreateErrorResponse(403, ex.Message, context.Logger);
        }
        catch (ValidationException ex)
        {
            return ResponseBuilder.CreateErrorCodeResponse(ex);
        }
    }
}

