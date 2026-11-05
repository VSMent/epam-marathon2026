import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { forkJoin } from 'rxjs';
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
export class GroupOrderPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(GroupOrdersService);
  private readonly toast = inject(ToastService);

  readonly reservationId = signal<string>('');
  readonly invite = signal<GuestInvite | null>(null);
  readonly participants = signal<Participant[]>([]);
  readonly ownerSessionId = signal<string | null>(null);
  readonly groupStatus = signal<'Open' | 'Submitted' | 'Closed'>('Open');

  readonly isLoading = signal(true);
  readonly error = signal<HttpErrorResponse | null>(null);
  readonly isModalOpen = signal(false);
  readonly isSubmitting = signal(false);


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

  loadData(): void {
    this.isLoading.set(true);
    this.error.set(null);

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
          this.isLoading.set(false);
        },
        error: (err: HttpErrorResponse) => {
          this.error.set(err);
          this.isLoading.set(false);
        },
      });
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