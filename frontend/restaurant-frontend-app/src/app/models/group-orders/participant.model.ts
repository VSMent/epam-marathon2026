export type ParticipantType = 'Owner' | 'Guest';

export interface BulkEquivalent {
  bulkDishId: string;
  bulkName: string;
  bulkPrice: number;
  minQuantity: number;
  servingNote: string;
}

export interface DishShort {
  id: string;
  name: string;
  price: number;
  imageUrl?: string;
  weight?: string;
  isAlcohol?: boolean;
  bulkEquivalent?: BulkEquivalent;
}

export interface ParticipantItem {
  dish: DishShort;
  quantity: number;
}

export interface Participant {
  id: string;
  type: ParticipantType;
  name: string;
  items: ParticipantItem[];
  total: number;
  isSubmitted: boolean;
  reminderSent?: boolean;
}

export interface BulkUpsellSuggestion {
  originalDishId: string;
  originalName: string;
  totalQuantity: number;
  originalSubtotal: number;
  bulkDishId: string;
  bulkName: string;
  bulkPrice: number;
  savings: number;
  servingNote: string;
}

