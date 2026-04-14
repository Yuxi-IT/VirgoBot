export interface Contact {
  id: number;
  name: string;
  email: string | null;
  phone: string | null;
  notes: string | null;
}

export interface ContactsResponse {
  success: boolean;
  data: Contact[];
}
