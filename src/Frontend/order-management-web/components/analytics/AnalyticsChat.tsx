"use client";

import { useEffect, useRef, useState } from "react";
import { AlertCircle, ArrowUp } from "lucide-react";
import { askAnalytics } from "@/lib/api";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

interface Turn {
  role: "user" | "assistant";
  text: string;
}

const SUGGESTIONS = [
  "Quantos pedidos temos hoje?",
  "Quantos pedidos estão pendentes?",
  "Qual o tempo médio para aprovar os pedidos?",
  "Qual o valor total de pedidos finalizados este mês?",
];

export function AnalyticsChat() {
  const [turns, setTurns] = useState<Turn[]>([]);
  const [question, setQuestion] = useState("");
  const [isAsking, setIsAsking] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const endRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (turns.length > 0) endRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [turns, isAsking]);

  const ask = async (text: string) => {
    const trimmed = text.trim();
    if (!trimmed || isAsking) return;

    setError(null);
    setQuestion("");
    setTurns((previous) => [...previous, { role: "user", text: trimmed }]);
    setIsAsking(true);

    try {
      const { answer } = await askAnalytics(trimmed);
      setTurns((previous) => [...previous, { role: "assistant", text: answer }]);
    } catch {
      setError("A pergunta não foi respondida. Tente de novo.");
    } finally {
      setIsAsking(false);
      inputRef.current?.focus();
    }
  };

  return (
    <div className="space-y-6">
      {turns.length === 0 ? (
        <div className="space-y-3">
          <p className="text-sm text-muted-foreground">Comece por uma destas:</p>
          <div className="flex flex-col items-start gap-2">
            {SUGGESTIONS.map((suggestion) => (
              <button
                key={suggestion}
                type="button"
                onClick={() => ask(suggestion)}
                className="rounded-md border bg-card px-3 py-2 text-left text-sm transition-colors hover:border-primary/40 hover:bg-accent/50"
              >
                {suggestion}
              </button>
            ))}
          </div>
        </div>
      ) : (
        <ul className="space-y-4">
          {turns.map((turn, index) => (
            <li
              key={index}
              className={cn(
                "max-w-[85%] text-sm",
                turn.role === "user"
                  ? "ml-auto rounded-lg bg-primary px-3 py-2 text-primary-foreground"
                  : "mr-auto whitespace-pre-wrap leading-relaxed"
              )}
            >
              {turn.text}
            </li>
          ))}
          {isAsking && (
            <li className="mr-auto inline-flex items-center gap-2 text-sm text-muted-foreground">
              <span className="size-1.5 rounded-full bg-stage-processing stage-breathe" />
              Consultando os pedidos
            </li>
          )}
          <div ref={endRef} />
        </ul>
      )}

      {error && (
        <p className="flex items-start gap-2 text-sm text-destructive">
          <AlertCircle className="mt-0.5 size-4 shrink-0" />
          {error}
        </p>
      )}

      <form
        onSubmit={(event) => {
          event.preventDefault();
          ask(question);
        }}
        className="flex gap-2"
      >
        <Input
          ref={inputRef}
          value={question}
          onChange={(event) => setQuestion(event.target.value)}
          maxLength={500}
          placeholder="Pergunte sobre os pedidos"
          aria-label="Pergunta sobre os pedidos"
        />
        <Button
          type="submit"
          size="icon"
          disabled={isAsking || question.trim().length === 0}
          aria-label="Enviar pergunta"
        >
          <ArrowUp className="size-4" />
        </Button>
      </form>
    </div>
  );
}
