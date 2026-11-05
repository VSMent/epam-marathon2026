import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { forkJoin, interval, Subscription } from 'rxjs';
import { switchMap } from 'rxjs/operators';

import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { Participant } from '@app/models/group-orders/participant.model';

@Component({
  selector: 'app-group-order-page',
  imports: [ParticipantCardComponent, ModalComponent, CurrencyPipe, DatePipe],
  styleUrl: './group-order-page.component.scss',
  templateUrl: './group-order-page.component.html',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(GroupOrdersService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);

  readonly reservationId = signal<string>('');
  readonly invite = signal<GuestInvite | null>(null);
  readonly participants = signal<Participant[]>([]);
  readonly ownerSessionId = signal<string | null>(null);
  readonly groupStatus = signal<'Open' | 'Submitted' | 'Closed'>('Open');

  readonly isLoading = signal(true);
  readonly error = signal<HttpErrorResponse | null>(null);
  readonly isModalOpen = signal(false);
  readonly isSubmitting = signal(false);

  readonly lastUpdatedAt = signal<Date | null>(null);
  readonly refreshError = signal(false);

  private pollingSub?: Subscription;

  readonly consolidatedOrder = computed(() => consolidatePreOrder(this.participants()));

  readonly total = computed(() => this.consolidatedOrder().total);

  readonly unsubmittedCount = computed(() =>
    this.participants().filter(p => !p.isSubmitted).length
  );

  readonly hasAnyDishes = computed(() =>
    this.participants().some(p => p.items.length > 0)
  );

  readonly canSubmit = computed(() =>
    this.consolidatedOrder().items.length > 0 && !this.isSubmitting()
  );

  ngOnInit(): void {
    const resId = this.route.snapshot.paramMap.get('reservationId') ?? '';
    this.reservationId.set(resId);
    this.loadData();
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  loadData(): void {
    this.stopPolling();
    this.isLoading.set(true);
    this.error.set(null);
    this.refreshError.set(false);

    const resId = this.reservationId();

    forkJoin({
      invite: this.service.getGroupInvite$(resId),
      ownerSessionId: this.service.getOwnerSessionId$(resId),
    })
      .pipe(
        switchMap(({ invite, ownerSessionId }) => {
          this.invite.set(invite);
          this.ownerSessionId.set(ownerSessionId);

          return forkJoin({
            sessions: this.service.getSessions$(invite.GroupReservationId),
            status: this.service.getStatus$(invite.GroupReservationId),
          });
        })
      )
      .subscribe({
        next: ({ sessions, status }) => {
          this.participants.set(sessions.participants);
          this.groupStatus.set(status.status);
          this.lastUpdatedAt.set(new Date());
          this.isLoading.set(false);
          this.startPolling();
        },
        error: (err: HttpErrorResponse) => {
          this.error.set(err);
          this.isLoading.set(false);
        },
      });
  }

  refresh(options?: { silent?: boolean }): void {
    const inv = this.invite();
    if (!inv || this.groupStatus() === 'Submitted') {
      return;
    }

    forkJoin({
      sessions: this.service.getSessions$(inv.GroupReservationId, options),
      status: this.service.getStatus$(inv.GroupReservationId, options),
    }).subscribe({
      next: ({ sessions, status }) => {
        this.participants.set(sessions.participants);
        this.groupStatus.set(status.status);
        this.lastUpdatedAt.set(new Date());
        this.refreshError.set(false);
        if (status.status === 'Submitted') {
          this.stopPolling();
        }
      },
      error: () => {
        this.refreshError.set(true);
      },
    });
  }

  private startPolling(): void {
    this.stopPolling();
    if (this.groupStatus() === 'Submitted') {
      return;
    }

    this.pollingSub = interval(15_000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.refresh({ silent: true });
      });
  }

  private stopPolling(): void {
    if (this.pollingSub) {
      this.pollingSub.unsubscribe();
      this.pollingSub = undefined;
    }
  }

  openModal(): void {
    this.isModalOpen.set(true);
  }

  closeModal(): void {
    if (!this.isSubmitting()) {
      this.isModalOpen.set(false);
    }
  }

  confirmSubmit(): void {
    const invite = this.invite();
    if (!invite || this.isSubmitting()) return;

    this.isSubmitting.set(true);

    this.service.submit$(invite.GroupReservationId, this.reservationId()).subscribe({
      next: () => {
        this.groupStatus.set('Submitted');
        this.stopPolling();
        this.isSubmitting.set(false);
        this.isModalOpen.set(false);
        this.toast.success('Pre-order sent to the restaurant');
      },
      error: () => {
        this.isSubmitting.set(false);
      },
    });
  }
}