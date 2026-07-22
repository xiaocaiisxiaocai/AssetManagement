export function isFutureFollowupDate(date: Date, today = new Date()) {
  const candidate = new Date(
    date.getFullYear(),
    date.getMonth(),
    date.getDate(),
  );
  const current = new Date(
    today.getFullYear(),
    today.getMonth(),
    today.getDate(),
  );
  return candidate.getTime() > current.getTime();
}

export function localTodayText(today = new Date()) {
  const year = today.getFullYear();
  const month = `${today.getMonth() + 1}`.padStart(2, '0');
  const day = `${today.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
}
