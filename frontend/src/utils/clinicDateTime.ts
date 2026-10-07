const clinicTimeZone = import.meta.env.VITE_CLINIC_TIME_ZONE?.trim() || 'America/Cuiaba';

const partsFormatter = new Intl.DateTimeFormat('en-CA', {
  timeZone: clinicTimeZone,
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

const displayFormatter = new Intl.DateTimeFormat('pt-BR', {
  timeZone: clinicTimeZone,
  dateStyle: 'short',
  timeStyle: 'short',
});

const dateOnlyFormatter = new Intl.DateTimeFormat('pt-BR', {
  timeZone: clinicTimeZone,
  dateStyle: 'short',
});

type ClinicDateTimeParts = { year: string; month: string; day: string; hour: string; minute: string };

const getClinicParts = (value: string | Date): ClinicDateTimeParts => {
  const parts = partsFormatter.formatToParts(value instanceof Date ? value : new Date(value));
  const values = Object.fromEntries(parts.map(part => [part.type, part.value]));
  return {
    year: values.year,
    month: values.month,
    day: values.day,
    hour: values.hour,
    minute: values.minute,
  };
};

export const clinicDateKey = (value: string | Date) => {
  const parts = getClinicParts(value);
  return `${parts.year}-${parts.month}-${parts.day}`;
};

export const clinicTimeKey = (value: string | Date) => {
  const parts = getClinicParts(value);
  return `${parts.hour}:${parts.minute}`;
};

export const clinicDateTimeInputValue = (value: string | Date) => `${clinicDateKey(value)}T${clinicTimeKey(value)}`;
export const clinicTodayKey = () => clinicDateKey(new Date());
export const formatClinicDateTime = (value: string | Date) => displayFormatter.format(value instanceof Date ? value : new Date(value));
export const formatClinicDate = (value: string | Date) => dateOnlyFormatter.format(value instanceof Date ? value : new Date(value));
