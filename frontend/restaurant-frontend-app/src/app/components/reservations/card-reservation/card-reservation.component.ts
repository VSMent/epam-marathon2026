import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { Params, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { CreateOrderRequest } from '@app/models/cart/create-order-request.model';
import {
  ReservationCustomer,
  ReservationStatusCustomer,
} from '@app/models/reservations/reservation.model';
import { PlaceOrderWaiterComponent } from '@app/components/place-order-waiter/place-order-waiter.component';
import { Roles } from '@app/shared/constants/roles';
import { GuestInviteService } from '@app/services/reservation/guest-invite.service';
import { ToastService } from '@app/services/toast/toast.service';
import { CustomSelectComponent } from '@app/shared/custom-components/custom-select/custom-select.component';
import { getServerErrorMessage } from '@app/shared/helpers/http-error-helpers';

interface ReservationStatusView {
  class: string;
  label: string;
}

interface WaiterPreOrderNavigationData {
  reservationId: string;
  createdByName: string;
  createdByRole: string;
  tableName: string;
}

@Component({
  selector: 'app-card-reservation',
  standalone: true,
  imports: [
    DatePipe,
    CustomSelectComponent,
    PlaceOrderWaiterComponent,
    RouterLink,
  ],
  templateUrl: './card-reservation.component.html',
  styleUrl: './card-reservation.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CardReservationComponent {
  private readonly router = inject(Router);
  private readonly guestInviteService = inject(GuestInviteService);
  private readonly toast = inject(ToastService);
  readonly Roles = Roles;
  readonly ReservationStatusCustomer = ReservationStatusCustomer;

  readonly r = input.required<ReservationCustomer>();
  readonly role = input<string>(Roles.CUSTOMER);
  readonly statusEnum =
    input<Record<number, ReservationStatusView>>({});

  readonly availableTableOptions = input<string[]>([]);

  readonly availableTableOptionsMap =
    input<Map<string, string>>(new Map<string, string>());

  readonly selectedTableLabel = input('');

  readonly createdByName = input<string | undefined>();

  readonly createdByRole =
    input<ReservationCustomer['createdByRole']>();
  readonly tableChanged = output<{
    reservationId: string;
    newTableId: string;
  }>();

  readonly cancelReservation = output<string>();

  readonly editReservation =
    output<ReservationCustomer>();

  readonly postponeReservation =
    output<ReservationCustomer>();

  readonly leaveFeedbackReservation =
    output<ReservationCustomer>();

  readonly updateFeedbackReservation =
    output<ReservationCustomer>();

  readonly sendAnonymousFeedback =
    output<ReservationCustomer>();

  readonly statusChanged = output<{
    id: string;
    newStatus: ReservationStatusCustomer;
  }>();

  readonly preOrder = output<CreateOrderRequest>();
  readonly showWaiterOrderModal = signal(false);

  readonly statusView = computed<ReservationStatusView>(() => {
    const reservation = this.r();

    return (
      this.statusEnum()[reservation.status] ?? {
        class: '',
        label: 'Unknown',
      }
    );
  });

  readonly displayedTableLabel = computed(() => {
    return this.selectedTableLabel() || 'Table (Unknown)';
  });

  readonly creatorName = computed(() => {
    return (
      this.createdByName() ||
      this.r().createdByName ||
      'Unknown'
    );
  });

  readonly creatorRole = computed(() => {
    return (
      this.createdByRole() ||
      this.r().createdByRole ||
      Roles.CUSTOMER
    );
  });

  readonly preOrderItemCount = computed(() => {
    return this.r().preOrder?.orderItems?.length ?? 0;
  });

  readonly canOpenWaiterPreOrder = computed(() => {
    return (
      this.preOrderItemCount() > 0 ||
      this.creatorRole() === Roles.WAITER
    );
  });

  // The group pre-order page only exists for group reservations (2+ guests).
  // Owner-only: fetches the group invite token and copies the join link to the clipboard.
  // The clipboard write must start inside the click; a write issued after the HTTP call is
  // rejected by the browser. ClipboardItem accepts a promise, so the write stays in the gesture.
  copyInviteLink(): void {
    // The clipboard rejects the promise without the original error, so keep the HTTP error aside
    // to show the real reason (e.g. the group pre-order was already submitted).
    let inviteError: unknown;

    const linkBlob = firstValueFrom(this.guestInviteService.getInviteToken$(this.r().id))
      .then(({ token }) => new Blob([`${window.location.origin}/join/${token}`], { type: 'text/plain' }))
      .catch((error: unknown) => {
        inviteError = error;
        throw error;
      });

    navigator.clipboard
      .write([new ClipboardItem({ 'text/plain': linkBlob })])
      .then(() => this.toast.success('Share it with your friends.', 'Link copied!'))
      .catch(() => this.toast.error(this.copyInviteErrorMessage(inviteError)));
  }

  private copyInviteErrorMessage(error: unknown): string {
    const serverMessage = error instanceof HttpErrorResponse ? getServerErrorMessage(error) : '';

    return serverMessage
      ? `Could not copy the link: ${serverMessage}`
      : 'Could not copy the link. Please try again later.';
  }

  readonly canOpenGroupOrder = computed(() => {
    return (
      this.r().guestCount >= 2 &&
      this.r().status !== ReservationStatusCustomer.Cancelled
    );
  });

  // The invite link stops working once the group pre-order is submitted (backend decides).
  readonly canCopyInviteLink = computed(() => {
    return this.canOpenGroupOrder() && this.r().guestInviteLinkAvailable === true;
  });

  readonly isAnonymous = computed(() => {
    return !this.r().customerId;
  });

  readonly hasFullFeedback = computed(() => {
    const feedback = this.r().feedback;

    return (
      feedback?.cuisineExperience != null &&
      feedback?.service != null
    );
  });

  onPreOrder(): void {
    const reservation = this.r();
    const startTime = new Date(reservation.startTime);
    const endTime = new Date(reservation.endTime);

    const tableLabel =
      reservation.tableName ||
      `Table ${reservation.tableId}`;

    const queryParams: Params = {
      preOrder: 'true',
      reservationId: reservation.id,
      reservationLabel: [
        reservation.locationAddress,
        tableLabel,
        startTime.toDateString(),
        `${startTime.toLocaleTimeString()} - ${endTime.toLocaleTimeString()}`,
      ].join(', '),
    };

    void this.router.navigate(['/menu'], {
      queryParams,
    });
  }
  onPreOrderWaiter(): void {
    if (this.preOrderItemCount() > 0) {
      this.navigateToWaiterPreOrder();
      return;
    }

    if (this.creatorRole() === Roles.WAITER) {
      this.showWaiterOrderModal.set(true);
    }
  }

  closeWaiterOrderModal(): void {
    this.showWaiterOrderModal.set(false);
  }

  onWaiterOrderCreated(order: CreateOrderRequest): void {
    this.showWaiterOrderModal.set(false);
    this.preOrder.emit(order);
  }

  onTableChange(label: string): void {
    const reservation = this.r();
    const newTableId =
      this.availableTableOptionsMap().get(label);

    if (!newTableId || newTableId === reservation.tableId) {
      return;
    }

    this.tableChanged.emit({
      reservationId: reservation.id,
      newTableId,
    });
  }

  onStatusChange(
    newStatus: ReservationStatusCustomer,
  ): void {
    this.statusChanged.emit({
      id: this.r().id,
      newStatus,
    });
  }

  private navigateToWaiterPreOrder(): void {
    const reservation = this.r();

    const navigationData: WaiterPreOrderNavigationData = {
      reservationId: reservation.id,
      createdByName: this.creatorName(),
      createdByRole: this.creatorRole(),
      tableName: reservation.tableName || 'Unknown table',
    };

    sessionStorage.setItem(
      'waiterPreOrderData',
      JSON.stringify(navigationData),
    );

    void this.router.navigate(['/waiter-pre-order'], {
      state: navigationData,
    });
  }
}
