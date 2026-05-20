import { deleteLicencesCheckoutsSeatId } from './generated/api';

export async function checkinSeat(seatId: string): Promise<void> {
  await deleteLicencesCheckoutsSeatId(seatId);
}
