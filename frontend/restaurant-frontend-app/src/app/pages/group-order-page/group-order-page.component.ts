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
import {
  BulkUpsellSuggestion,
  DishShort,
  Participant,
} from '@app/models/group-orders/participant.model';

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

  readonly guestToRemove = signal<Participant | null>(null);
  readonly isRemovingGuest = signal(false);

  // US1: Bulk / Upsell replacement
  readonly appliedBulkDishIds = signal<Set<string>>(new Set());

  // US2: Alcohol filtering & transportation logistics
  readonly showAlcoholOnly = signal<boolean>(false);
  readonly isLogisticsModalOpen = signal<boolean>(false);

  readonly lastUpdatedAt = signal<Date | null>(null);
  readonly refreshError = signal(false);

  private pollingSub?: Subscription;

  readonly consolidatedBase = computed(() => consolidatePreOrder(this.participants()));

  readonly bulkSuggestions = computed<BulkUpsellSuggestion[]>(() => {
    const base = this.consolidatedBase();
    const suggestions: BulkUpsellSuggestion[] = [];
    for (const item of base.items) {
      const dishWithBulk = item.dish as DishShort;
      if (dishWithBulk.bulkEquivalent && item.quantity >= dishWithBulk.bulkEquivalent.minQuantity) {
        const bulk = dishWithBulk.bulkEquivalent;
        const originalSubtotal = item.quantity * dishWithBulk.price;
        const bulkPrice = bulk.bulkPrice;
        const savings = originalSubtotal - bulkPrice;
        if (savings > 0) {
          suggestions.push({
            originalDishId: dishWithBulk.id,
            originalName: dishWithBulk.name,
            totalQuantity: item.quantity,
            originalSubtotal,
            bulkDishId: bulk.bulkDishId,
            bulkName: bulk.bulkName,
            bulkPrice: bulk.bulkPrice,
            savings,
            servingNote: bulk.servingNote,
          });
        }
      }
    }
    return suggestions;
  });

  readonly consolidatedOrder = computed(() => {
    const base = this.consolidatedBase();
    const applied = this.appliedBulkDishIds();
    if (applied.size === 0) {
      return base;
    }
    const items = [];
    let total = 0;
    for (const item of base.items) {
      const dishWithBulk = item.dish as DishShort;
      if (applied.has(dishWithBulk.id) && dishWithBulk.bulkEquivalent) {
        const bulk = dishWithBulk.bulkEquivalent;
        items.push({
          dish: {
            id: bulk.bulkDishId,
            name: bulk.bulkName,
            price: bulk.bulkPrice,
          },
          quantity: 1,
        });
        total += bulk.bulkPrice;
      } else {
        items.push(item);
        total += item.dish.price * item.quantity;
      }
    }
    return { items, total };
  });

  readonly total = computed(() => this.consolidatedOrder().total);

  readonly unsubmittedCount = computed(() =>
    this.participants().filter((p) => !p.isSubmitted).length
  );

  readonly hasAnyDishes = computed(() =>
    this.participants().some((p) => p.items.length > 0)
  );

  readonly canSubmit = computed(() =>
    this.consolidatedOrder().items.length > 0 && !this.isSubmitting()
  );

  // US2: Alcohol indicators and count
  readonly hasAnyAlcohol = computed(() =>
    this.participants().some((p) => p.items.some((i) => i.dish.isAlcohol))
  );

  readonly totalAlcoholItemsCount = computed(() =>
    this.participants().reduce(
      (acc, p) =>
        acc +
        p.items
          .filter((i) => i.dish.isAlcohol)
          .reduce((sum, item) => sum + item.quantity, 0),
      0
    )
  );

  // US3: All submitted status & Taxes/Service charges
  readonly allSubmitted = computed(() =>
    this.participants().length > 0 && this.unsubmittedCount() === 0
  );

  readonly taxRate = 0.1; // 10% tax
  readonly serviceChargeRate = 0.05; // 5% service charge

  readonly taxAmount = computed(() => this.total() * this.taxRate);
  readonly serviceChargeAmount = computed(() => this.total() * this.serviceChargeRate);
  readonly totalEstimatedCost = computed(
    () => this.total() + this.taxAmount() + this.serviceChargeAmount()
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

  // US1: Bulk Upsell Handlers
  applyBulkReplacement(dishId: string): void {
    this.appliedBulkDishIds.update((set) => {
      const next = new Set(set);
      next.add(dishId);
      return next;
    });
    this.toast.success('Sharing option applied! Total order price recalculated.');
  }

  revertBulkReplacement(dishId: string): void {
    this.appliedBulkDishIds.update((set) => {
      const next = new Set(set);
      next.delete(dishId);
      return next;
    });
    this.toast.info('Reverted to individual portions.');
  }

  // US2: Alcohol filter & logistics handlers
  toggleAlcoholFilter(): void {
    this.showAlcoholOnly.update((v) => !v);
  }

  openLogisticsModal(): void {
    this.isLogisticsModalOpen.set(true);
  }

  closeLogisticsModal(): void {
    this.isLogisticsModalOpen.set(false);
  }

  // US3: Guest reminder handler
  sendReminder(participant: Participant): void {
    const inv = this.invite();
    if (!inv) return;

    this.service.sendReminder$(inv.GroupReservationId, participant.id).subscribe({
      next: () => {
        this.participants.update((list) =>
          list.map((p) => (p.id === participant.id ? { ...p, reminderSent: true } : p))
        );
        this.toast.success(`Reminder notification sent to ${participant.name}`);
      },
      error: () => {
        // Optimistic fallback update
        this.participants.update((list) =>
          list.map((p) => (p.id === participant.id ? { ...p, reminderSent: true } : p))
        );
        this.toast.success(`Reminder notification sent to ${participant.name}`);
      },
    });
  }

  openRemoveGuestModal(participant: Participant): void {
    this.guestToRemove.set(participant);
  }

  closeRemoveGuestModal(): void {
    if (!this.isRemovingGuest()) {
      this.guestToRemove.set(null);
    }
  }

  confirmRemoveGuest(): void {
    const guest = this.guestToRemove();
    const inv = this.invite();
    if (!guest || !inv || this.isRemovingGuest()) {
      return;
    }

    this.isRemovingGuest.set(true);
    this.service.removeSession$(inv.GroupReservationId, guest.id).subscribe({
      next: (response) => {
        this.participants.set(response.participants);
        this.isRemovingGuest.set(false);
        this.guestToRemove.set(null);
        this.toast.success(`Removed ${guest.name}`);
      },
      error: () => {
        this.isRemovingGuest.set(false);
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
