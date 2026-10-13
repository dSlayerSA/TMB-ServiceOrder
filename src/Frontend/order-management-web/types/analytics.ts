export interface AnalyticsAnswer {
  answer: string;
  toolsUsed: string[];
}

export interface AnalyticsStatus {
  enabled: boolean;
  /** Which backend is answering, e.g. "Claude (claude-opus-5)" or "Ollama (qwen2.5:3b)". */
  provider: string;
}
