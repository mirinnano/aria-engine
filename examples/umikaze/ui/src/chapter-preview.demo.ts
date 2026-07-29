import type { ChapterPreviewRecord } from "./chapter-preview.types";

// Textual chapter identity is authored in Aria choice labels. This map owns
// only the edition-specific photograph, so TSX cannot silently diverge from
// the script's numeral, proposition, date, or invitation.
export const chapterPreviewByLabel: Record<string, ChapterPreviewRecord> = {
  PROLOGUE: {
    scene: "ward",
  },
  "DAY 1": {
    scene: "station",
  },
  "DAY 2": {
    scene: "rain",
  },
  "DAY 3": {
    scene: "rail-sunset",
  },
  "DAY 4": {
    scene: "shore",
  },
};
