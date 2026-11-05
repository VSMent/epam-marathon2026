import { inject, Injectable } from '@angular/core';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { OwnerSessionResponse } from '@app/models/group-orders/owner-session.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { Observable, map } from 'rxjs';
import { HttpClient, HttpContext } from '@angular/common/http';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import { Participant, ParticipantItem, ParticipantType } from '@app/models/group-orders/participant.model';

// GET /guest/groups/{id}/sessions returns PascalCase keys at the top level and per participant.
// The UI model is camelCase, so normalise here. camelCase input is accepted too (mock layer).
interface RawParticipant {
  id?: string;
  Id?: string;
  type?: ParticipantType;
  Type?: ParticipantType;
  name?: string;
  Name?: string;
  items?: ParticipantItem[];
  Items?: ParticipantItem[];
  total?: number;
  Total?: number;
  isSubmitted?: boolean;
  IsSubmitted?: boolean;
  reminderSent?: boolean;
  ReminderSent?: boolean;
}

interface RawGroupSessionsResponse {
  participants?: RawParticipant[];
  Participants?: RawParticipant[];
  total?: number;
  Total?: number;
}

function toParticipant(raw: RawParticipant): Participant {
  return {
    id: raw.id ?? raw.Id ?? '',
    type: raw.type ?? raw.Type ?? 'Guest',
    name: raw.name ?? raw.Name ?? '',
    items: raw.items ?? raw.Items ?? [],
    total: raw.total ?? raw.Total ?? 0,
    isSubmitted: raw.isSubmitted ?? raw.IsSubmitted ?? false,
    reminderSent: raw.reminderSent ?? raw.ReminderSent,
  };
}

function toSessionsResponse(raw: RawGroupSessionsResponse): GroupSessionsResponse {
  return {
    participants: (raw.participants ?? raw.Participants ?? []).map(toParticipant),
    total: raw.total ?? raw.Total ?? 0,
  };
}

@Injectable({ providedIn: 'root' })
export class GroupOrdersService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  // Owner-only and authenticated. Not capped by the guest limit, so the owner can open a full group.
  getGroupInvite$(reservationId: string): Observable<GuestInvite> {
    return this.http.get<GuestInvite>(`${this.baseUrl}/reservations/${reservationId}/group-invite`);
  }

  getSessions$(
    groupReservationId: string,
    options?: { silent?: boolean }
  ): Observable<GroupSessionsResponse> {
    const context = options?.silent
      ? new HttpContext().set(SKIP_ERROR_TOAST, true)
      : new HttpContext();
    return this.http
      .get<RawGroupSessionsResponse>(`${this.baseUrl}/guest/groups/${groupReservationId}/sessions`, {
        context,
      })
      .pipe(map(toSessionsResponse));
  }

  getOwnerSessionId$(reservationId: string): Observable<string> {
    return this.http
      .get<OwnerSessionResponse>(`${this.baseUrl}/reservations/${reservationId}/guest-session-id`)
      .pipe(map((response) => response.guestSessionId));
  }

  getStatus$(
    groupReservationId: string,
    options?: { silent?: boolean }
  ): Observable<GroupStatusResponse> {
    const context = options?.silent
      ? new HttpContext().set(SKIP_ERROR_TOAST, true)
      : new HttpContext();
    return this.http.get<GroupStatusResponse>(
      `${this.baseUrl}/guest/groups/${groupReservationId}/status`,
      { context }
    );
  }

  submit$(groupReservationId: string, reservationId: string): Observable<unknown> {
    return this.http.post(
      `${this.baseUrl}/group-reservations/${groupReservationId}/submit`,
      { reservationId }
    );
  }

  // Owner-only: reopens the owner's own pre-order (submitted -> editable) before they edit it from the menu.
  unsubmitOwnPreOrder$(reservationId: string): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/orders/${reservationId}/unsubmit`, {});
  }

  removeSession$(
    groupReservationId: string,
    guestSessionId: string
  ): Observable<GroupSessionsResponse> {
    return this.http
      .delete<RawGroupSessionsResponse>(
        `${this.baseUrl}/guest/groups/${groupReservationId}/sessions/${guestSessionId}`
      )
      .pipe(map(toSessionsResponse));
  }

  sendReminder$(
    groupReservationId: string,
    guestSessionId: string
  ): Observable<{ success: boolean; message: string }> {
    return this.http.post<{ success: boolean; message: string }>(
      `${this.baseUrl}/guest/groups/${groupReservationId}/sessions/${guestSessionId}/remind`,
      {}
    );
  }
}