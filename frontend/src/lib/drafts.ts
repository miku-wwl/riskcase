import { useEffect, useRef, useState } from "react";

export function useDraft(key: string, initialValue = "") {
  const [value, setValue] = useState(() => {
    try {
      return localStorage.getItem(key) ?? initialValue;
    } catch {
      return initialValue;
    }
  });
  const valueRef = useRef(value);
  valueRef.current = value;

  useEffect(() => {
    try {
      if (value) localStorage.setItem(key, value);
      else localStorage.removeItem(key);
    } catch {
      // The form remains editable when browser storage is unavailable.
    }
  }, [key, value]);

  const clearIfUnchanged = (submitted: string) => {
    if (valueRef.current === submitted) setValue("");
  };

  return { value, setValue, clearIfUnchanged };
}

export function draftKey(caseId: string, userId: string, field: string) {
  return `riskcase:draft:${userId}:${caseId}:${field}`;
}
