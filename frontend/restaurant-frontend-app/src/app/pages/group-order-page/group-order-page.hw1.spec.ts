import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupOrderPageComponent } from './group-order-page.component';

const springSalad = {
  id: 'dish-spring',
  name: 'Spring Salad',
  price: 10,
  bulkEquivalent: {
    bulkDishId: 'dish-spring-family',
    bulkName: 'Family Sharing Spring Salad Bowl (Serves 3)',
    bulkPrice: 22,
    minQuantity: 3,
    servingNote: 'Combine 3 portions into 1 Large Sharing Platter',
  },
};

const wine = {
  id: 'dish-wine',
  name: 'Pinot Noir (Glass)',
  price: 12,
  isAlcohol: true,
};

const steak = {
  id: 'dish-steak',
  name: 'Ribeye Steak',
  price: 30,
};

const INVITE = {
  GroupReservationId: 'grp-hw1',
  LocationAddress: 'Khreshchatyk St 15, Kyiv',
  MaxGuestCount: 5,
  StartTime: '2026-11-20T18:00:00Z',
  EndTime: '2026-11-20T20:00:00Z',
} as GuestInvite;

describe('Homework 1 · Group Pre-Order (US1, US2, US3)', () => {
  let service: jasmine.SpyObj<GroupOrdersService>;
  let toast: jasmine.SpyObj<ToastService>;

  function createComponent(sessions: GroupSessionsResponse) {
    service = jasmine.createSpyObj<GroupOrdersService>('GroupOrdersService', [
      'getGroupInvite$',
      'getSessions$',
      'getOwnerSessionId$',
      'getStatus$',
      'submit$',
      'sendReminder$',
    ]);
    toast = jasmine.createSpyObj<ToastService>('ToastService', ['success', 'info', 'error']);

    service.getGroupInvite$.and.returnValue(of(INVITE));
    service.getSessions$.and.returnValue(of(structuredClone(sessions)));
    service.getOwnerSessionId$.and.returnValue(of('owner'));
    service.getStatus$.and.returnValue(
      of({ currentParticipants: sessions.participants.length, maxCapacity: 5, status: 'Open' })
    );
    service.sendReminder$.and.returnValue(of({ success: true, message: 'Dispatched' }));

    TestBed.configureTestingModule({
      imports: [GroupOrderPageComponent],
      providers: [
        { provide: GroupOrdersService, useValue: service },
        { provide: ToastService, useValue: toast },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ reservationId: 'res-hw1' }) } } },
      ],
    });

    const fixture = TestBed.createComponent(GroupOrderPageComponent);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, element: fixture.nativeElement as HTMLElement };
  }

  describe('US1 · Bulk / Upsell Optimization', () => {
    it('AC1: displays bulk suggestion when multiple guests select >= minQuantity of identical item', () => {
      const sessions: GroupSessionsResponse = {
        participants: [
          { id: 'owner', type: 'Owner', name: 'Yevhenii', isSubmitted: true, total: 10, items: [{ dish: springSalad, quantity: 1 }] },
          { id: 'guest1', type: 'Guest', name: 'Olena', isSubmitted: true, total: 20, items: [{ dish: springSalad, quantity: 2 }] },
        ],
        total: 30,
      };

      const { element } = createComponent(sessions);
      const text = element.textContent ?? '';

      expect(text).toContain('Bulk & Sharing Optimization');
      expect(text).toContain('Combine 3 portions into 1 Large Sharing Platter');
      expect(text).toContain('Save $8.00'); // 3 * 10 - 22 = 8
    });

    it('AC2: updates consolidated order and recalculates total price when organizer applies bulk replacement', () => {
      const sessions: GroupSessionsResponse = {
        participants: [
          { id: 'owner', type: 'Owner', name: 'Yevhenii', isSubmitted: true, total: 10, items: [{ dish: springSalad, quantity: 1 }] },
          { id: 'guest1', type: 'Guest', name: 'Olena', isSubmitted: true, total: 20, items: [{ dish: springSalad, quantity: 2 }] },
        ],
        total: 30,
      };

      const { fixture, element } = createComponent(sessions);
      const applyBtn = element.querySelector('.apply-bulk-btn') as HTMLButtonElement;
      expect(applyBtn).toBeTruthy();

      applyBtn.click();
      fixture.detectChanges();

      const text = element.textContent ?? '';
      expect(text).toContain('Family Sharing Spring Salad Bowl (Serves 3) × 1');
      expect(text).toContain('$22.00');
      expect(toast.success).toHaveBeenCalledWith(jasmine.stringMatching(/Sharing option applied/));
    });

    it('AC3: shows no optimization hints when guests select distinct items or items without bulk equivalents', () => {
      const sessions: GroupSessionsResponse = {
        participants: [
          { id: 'owner', type: 'Owner', name: 'Yevhenii', isSubmitted: true, total: 30, items: [{ dish: steak, quantity: 1 }] },
          { id: 'guest1', type: 'Guest', name: 'Olena', isSubmitted: true, total: 12, items: [{ dish: wine, quantity: 1 }] },
        ],
        total: 42,
      };

      const { element } = createComponent(sessions);
      expect(element.querySelector('.bulk-optimization-card')).toBeNull();
    });
  });

  describe('US2 · Alcohol Filtering & Logistics Planning', () => {
    const sessionsWithAlcohol: GroupSessionsResponse = {
      participants: [
        { id: 'owner', type: 'Owner', name: 'Yevhenii', isSubmitted: true, total: 30, items: [{ dish: steak, quantity: 1 }] },
        { id: 'guest1', type: 'Guest', name: 'Olena', isSubmitted: true, total: 12, items: [{ dish: wine, quantity: 1 }] },
      ],
      total: 42,
    };

    it('AC1: displays alcohol indicator badge on guest card and item', () => {
      const { element } = createComponent(sessionsWithAlcohol);
      const text = element.textContent ?? '';
      expect(text).toContain('Alcohol');
      expect(element.querySelector('.alcohol-chip')).toBeTruthy();
      expect(element.querySelector('.alcohol-tag')?.textContent).toContain('🍷');
    });

    it('AC2: toggles Show Alcohol Only filter and shows only alcoholic drinks', () => {
      const { fixture, element } = createComponent(sessionsWithAlcohol);
      const toggleBtn = element.querySelector('.filter-toggle-btn') as HTMLButtonElement;
      expect(toggleBtn).toBeTruthy();

      toggleBtn.click();
      fixture.detectChanges();

      expect(toggleBtn.classList).toContain('active');
      const cards = element.querySelectorAll('app-participant-card');
      expect(cards[0].textContent).toContain('No alcoholic drinks in this pre-order');
      expect(cards[1].textContent).toContain('Pinot Noir (Glass)');
    });

    it('AC3: presents Plan Logistics prompt with taxi options pre-filled with restaurant address', () => {
      const { fixture, element } = createComponent(sessionsWithAlcohol);
      const logisticsBtn = element.querySelector('.plan-logistics-btn') as HTMLButtonElement;
      expect(logisticsBtn).toBeTruthy();

      logisticsBtn.click();
      fixture.detectChanges();

      const modal = element.querySelector('[role="dialog"]');
      expect(modal).toBeTruthy();
      expect(modal?.textContent).toContain('Plan Transportation Logistics');
      expect(modal?.textContent).toContain('Khreshchatyk St 15, Kyiv');
      expect(modal?.textContent).toContain('Uber');
      expect(modal?.textContent).toContain('Bolt');
      expect(modal?.textContent).toContain('Uklon');
    });
  });

  describe('US3 · Consolidated Summary, Estimated Taxes & Reminders', () => {
    it('AC2: displays taxes (10%), service charges (5%) and grand total when all guests submit', () => {
      const allSubmittedSessions: GroupSessionsResponse = {
        participants: [
          { id: 'owner', type: 'Owner', name: 'Yevhenii', isSubmitted: true, total: 100, items: [{ dish: steak, quantity: 2 }, { dish: wine, quantity: 3.3333 }] },
          { id: 'guest1', type: 'Guest', name: 'Olena', isSubmitted: true, total: 100, items: [{ dish: springSalad, quantity: 10 }] },
        ],
        total: 200,
      };

      const { element } = createComponent(allSubmittedSessions);
      const text = element.textContent ?? '';

      expect(text).toContain('Items Subtotal:');
      expect(text).toContain('Estimated Taxes (10%):');
      expect(text).toContain('Service Charge (5%):');
      expect(text).toContain('Grand Total:');
      expect(text).toContain('$230.00'); // 200 + 20 + 10 = 230
    });

    it('AC3 & AC4: displays warning and Send Reminder button for empty guest, updates to sent on click', () => {
      const sessionsWithEmptyGuest: GroupSessionsResponse = {
        participants: [
          { id: 'owner', type: 'Owner', name: 'Yevhenii', isSubmitted: true, total: 30, items: [{ dish: steak, quantity: 1 }] },
          { id: 'guest-empty', type: 'Guest', name: 'Mariana', isSubmitted: false, total: 0, items: [] },
        ],
        total: 30,
      };

      const { fixture, element } = createComponent(sessionsWithEmptyGuest);
      const emptyGuestCard = element.querySelectorAll('app-participant-card')[1];
      expect(emptyGuestCard.textContent).toContain('No dishes yet');

      const reminderBtn = emptyGuestCard.querySelector('.reminder-btn') as HTMLButtonElement;
      expect(reminderBtn).toBeTruthy();
      expect(reminderBtn.textContent).toContain('Send Reminder');

      reminderBtn.click();
      fixture.detectChanges();

      expect(service.sendReminder$).toHaveBeenCalledWith('grp-hw1', 'guest-empty');
      expect(toast.success).toHaveBeenCalledWith(jasmine.stringMatching(/Reminder notification sent to Mariana/));
      expect(reminderBtn.textContent).toContain('Reminder Sent');
      expect(reminderBtn.disabled).toBeTrue();
    });
  });
});
