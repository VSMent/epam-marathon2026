import { inject, Injectable } from '@angular/core';
import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GroupInviteToken, GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupSessionsResponse  } from '@app/models/group-orders/group-sessions-response.model';
import { OwnerSessionResponse  } from '@app/models/group-orders/owner-session.model';
import { GroupStatusResponse  } from '@app/models/group-orders/group-status.model';
import { Observable, switchMap ,map} from 'rxjs';
import { HttpClient, HttpParams } from '@angular/common/http';


@Injectable({  providedIn: 'root' })
export class GroupOrdersService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  
  getGroupInvite$(reservationId: string): Observable<GuestInvite> {
    return this.http
      .get<GroupInviteToken>(`${this.baseUrl}/reservations/${reservationId}/invite-token`)
      .pipe(
        switchMap((response) =>
          this.http.get<GuestInvite>(`${this.baseUrl}/guest/invites/${response.token}`)
        )
      );
  }
  getSessions$(groupReservationId: string): Observable<GroupSessionsResponse> {
    return this.http.get<GroupSessionsResponse>(
      `${this.baseUrl}/guest/groups/${groupReservationId}/sessions`
    );
  }
  getOwnerSessionId$(reservationId: string): Observable<string> {
    return this.http
      .get<OwnerSessionResponse>(`${this.baseUrl}/reservations/${reservationId}/guest-session-id`)
      .pipe(map((response) => response.guestSessionId));
  }
  getStatus$(groupReservationId: string): Observable<GroupStatusResponse> {
    return this.http.get<GroupStatusResponse>(
      `${this.baseUrl}/guest/groups/${groupReservationId}/status`
    );
  }
  submit$(groupReservationId: string, reservationId: string): Observable<unknown> {
    return this.http.post(
      `${this.baseUrl}/group-reservations/${groupReservationId}/submit`,
      { reservationId }
    );
  }
}