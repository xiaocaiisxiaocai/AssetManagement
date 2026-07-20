import {
  businessDateText,
  calendarDateText,
  isValidCalendarDate,
} from './business-date';

export function isFutureReturnDate(value: string, now = new Date()) {
  return isValidCalendarDate(value) && value > businessDateText(now);
}

export function disableNonFutureReturnDate(time: Date, now = new Date()) {
  return calendarDateText(time) <= businessDateText(now);
}

export function isValidExtensionReturnDate(
  value: string,
  originalReturnDate: string,
  now = new Date(),
) {
  return (
    isFutureReturnDate(value, now) &&
    isValidCalendarDate(originalReturnDate) &&
    value > originalReturnDate
  );
}

export function disableNonExtensionReturnDate(
  time: Date,
  originalReturnDate: string,
  now = new Date(),
) {
  const today = businessDateText(now);
  const lowerBound =
    isValidCalendarDate(originalReturnDate) && originalReturnDate > today
      ? originalReturnDate
      : today;
  return calendarDateText(time) <= lowerBound;
}
