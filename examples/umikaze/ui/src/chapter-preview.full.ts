import { chapterPreviewByLabel as openingChapterPreviews } from "./chapter-preview.demo";
import type { ChapterPreviewRecord } from "./chapter-preview.types";

export const chapterPreviewByLabel: Record<string, ChapterPreviewRecord> = {
  ...openingChapterPreviews,
  "DAY 5": {
    scene: "rain-city",
  },
  "DAY 6": {
    scene: "platform",
  },
  "DAY 7": {
    scene: "mist",
  },
  "DAY 8": {
    scene: "understructure",
  },
  "DAY 9": {
    scene: "night",
  },
  "DAY 10": {
    scene: "blue",
  },
};
