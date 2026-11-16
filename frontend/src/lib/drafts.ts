import { useRef, useState } from "react";

export type DraftSnapshot = { key: string; value: string; revision: string };

function read(key: string, fallback: string): DraftSnapshot {
  try {
    const stored = localStorage.getItem(key);
    if (stored) {
      try { const parsed = JSON.parse(stored); if (typeof parsed.value === "string" && typeof parsed.revision === "string") return { ...parsed, key }; } catch { /* Round 1 plain text. */ }
      return { key, value: stored, revision: crypto.randomUUID() };
    }
  } catch { /* Storage may be unavailable. */ }
  return { key, value: fallback, revision: crypto.randomUUID() };
}

export function useDraft(key: string, initialValue = "") {
  const [draft, setDraft] = useState(() => read(key, initialValue));
  const current = useRef(draft);
  if (current.current.key !== key) { current.current = read(key, initialValue); setDraft(current.current); }

  const setValue = (value: string) => {
    const next = { key, value, revision: crypto.randomUUID() };
    current.current = next;
    try { localStorage.setItem(key, JSON.stringify(next)); } catch { /* Keep editing in memory. */ }
    setDraft(next);
  };
  const capture = (): DraftSnapshot => ({ ...current.current });
  const clearIfUnchanged = (submitted: DraftSnapshot) => {
    if (submitted.key !== key || current.current.revision !== submitted.revision) return;
    try {
      const persisted = JSON.parse(localStorage.getItem(key) ?? "null");
      if (persisted && persisted.revision !== submitted.revision) return;
    } catch { /* In-memory generation still protects this tab. */ }
    setValue("");
  };
  return { value: current.current.value, setValue, capture, clearIfUnchanged };
}

export function draftKey(caseId: string, userId: string, field: string) {
  return `riskcase:draft:${userId}:${caseId}:${field}`;
}
