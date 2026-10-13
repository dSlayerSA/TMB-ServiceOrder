"use client";

import { useEffect, useState } from "react";
import { AlertCircle, Plus } from "lucide-react";
import { createOrder, fetchProducts } from "@/lib/api";
import type { OrderStatus, ProcessingOutcome } from "@/types/order";
import type { Product } from "@/types/product";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { StageMark, STAGE_COLOR } from "@/components/orders/StageMark";

const currency = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" });

const MIN_SECONDS = 1;
const MAX_SECONDS = 120;

const OUTCOMES: { value: ProcessingOutcome; label: string; ends: OrderStatus }[] = [
  { value: "Concluir", label: "Concluir", ends: "Finalizado" },
  { value: "Falhar", label: "Falhar", ends: "Falhou" },
  { value: "Cancelar", label: "Cancelar", ends: "Cancelado" },
];

/** One click each for the situations worth exercising repeatedly. */
const SCENARIOS: { label: string; seconds: number; outcome: ProcessingOutcome }[] = [
  { label: "Sucesso em 5s", seconds: 5, outcome: "Concluir" },
  { label: "Lento (30s)", seconds: 30, outcome: "Concluir" },
  { label: "Falha em 5s", seconds: 5, outcome: "Falhar" },
  { label: "Cancela após 10s", seconds: 10, outcome: "Cancelar" },
];

export function NewOrderDialog({ onCreated }: { onCreated: () => void }) {
  const [open, setOpen] = useState(false);
  const [products, setProducts] = useState<Product[] | null>(null);
  const [customer, setCustomer] = useState("");
  const [productId, setProductId] = useState("");
  const [seconds, setSeconds] = useState(5);
  const [outcome, setOutcome] = useState<ProcessingOutcome>("Concluir");
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Loaded when the dialog opens rather than with the page: the catalogue is
  // only needed once someone actually places an order.
  useEffect(() => {
    if (!open || products) return;
    fetchProducts()
      .then(setProducts)
      .catch(() => setError("O catálogo não carregou. Feche e tente de novo."));
  }, [open, products]);

  const selected = products?.find((p) => p.id === productId);
  const ending = OUTCOMES.find((o) => o.value === outcome)!.ends;
  const secondsValid = seconds >= MIN_SECONDS && seconds <= MAX_SECONDS;

  const reset = () => {
    setCustomer("");
    setProductId("");
    setSeconds(5);
    setOutcome("Concluir");
    setError(null);
  };

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!customer.trim() || !productId || !secondsValid || isSaving) return;

    setError(null);
    setIsSaving(true);
    try {
      await createOrder({
        customer: customer.trim(),
        productId,
        processingSeconds: seconds,
        outcome,
      });
      setOpen(false);
      reset();
      // The new order lands at the top of the list and starts moving on its own.
      onCreated();
    } catch {
      setError("O pedido não foi criado. Verifique a conexão e tente de novo.");
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) reset();
      }}
    >
      <DialogTrigger
        render={
          <Button size="sm">
            <Plus className="size-4" />
            Novo pedido
          </Button>
        }
      />

      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Novo pedido</DialogTitle>
          <DialogDescription>
            O valor vem do catálogo. O tempo e o desfecho controlam o worker.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={submit} className="space-y-5">
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="customer">Cliente</Label>
              <Input
                id="customer"
                value={customer}
                onChange={(event) => setCustomer(event.target.value)}
                placeholder="Maria Silva"
                maxLength={200}
                autoComplete="off"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="product">Produto</Label>
              {products === null ? (
                <Skeleton className="h-8 w-full" />
              ) : (
                <Select
                  items={products.map((p) => ({ value: p.id, label: p.name }))}
                  value={productId}
                  onValueChange={(value) => setProductId(value ?? "")}
                >
                  <SelectTrigger id="product" className="w-full">
                    <SelectValue placeholder="Escolha um produto" />
                  </SelectTrigger>
                  <SelectContent>
                    {products.map((product) => (
                      <SelectItem key={product.id} value={product.id}>
                        <span className="flex-1">{product.name}</span>
                        <span className="tabular font-mono text-xs text-muted-foreground">
                          {currency.format(product.price)}
                        </span>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            </div>
          </div>

          <div className="space-y-3 rounded-lg border bg-muted/40 p-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="text-sm font-medium">Cenário do worker</span>
              <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
                termina em
                <StageMark status={ending} className="size-2.5" />
                <span className={STAGE_COLOR[ending]}>{ending}</span>
              </span>
            </div>

            <div className="flex flex-wrap gap-1.5">
              {SCENARIOS.map((scenario) => {
                const active = scenario.seconds === seconds && scenario.outcome === outcome;
                return (
                  <button
                    key={scenario.label}
                    type="button"
                    onClick={() => {
                      setSeconds(scenario.seconds);
                      setOutcome(scenario.outcome);
                    }}
                    className={cn(
                      "rounded-full border px-2.5 py-1 text-xs transition-colors",
                      active
                        ? "border-primary bg-primary/10 text-foreground"
                        : "text-muted-foreground hover:border-primary/40 hover:text-foreground"
                    )}
                  >
                    {scenario.label}
                  </button>
                );
              })}
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <div className="space-y-2">
                <Label htmlFor="seconds">Tempo de processamento</Label>
                <div className="flex items-center gap-2">
                  <Input
                    id="seconds"
                    type="number"
                    min={MIN_SECONDS}
                    max={MAX_SECONDS}
                    value={seconds}
                    onChange={(event) => setSeconds(Number(event.target.value))}
                    aria-invalid={!secondsValid}
                    className="tabular font-mono"
                  />
                  <span className="text-sm text-muted-foreground">s</span>
                </div>
                {!secondsValid && (
                  <p className="text-xs text-destructive">
                    Informe de {MIN_SECONDS} a {MAX_SECONDS} segundos.
                  </p>
                )}
              </div>

              <div className="space-y-2">
                <Label htmlFor="outcome">Desfecho</Label>
                <Select
                  items={OUTCOMES.map((o) => ({ value: o.value, label: o.label }))}
                  value={outcome}
                  onValueChange={(value) => setOutcome((value as ProcessingOutcome) ?? "Concluir")}
                >
                  <SelectTrigger id="outcome" className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {OUTCOMES.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        <StageMark status={option.ends} className="size-2.5" />
                        <span className="flex-1">{option.label}</span>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
          </div>

          <div className="flex items-baseline justify-between border-t pt-4">
            <span className="text-sm text-muted-foreground">Valor</span>
            <span className="tabular font-mono text-lg">
              {selected ? currency.format(selected.price) : "—"}
            </span>
          </div>

          {error && (
            <p className="flex items-start gap-2 text-sm text-destructive">
              <AlertCircle className="mt-0.5 size-4 shrink-0" />
              {error}
            </p>
          )}

          <DialogFooter>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setOpen(false)}
              disabled={isSaving}
            >
              Cancelar
            </Button>
            <Button
              type="submit"
              size="sm"
              disabled={isSaving || !customer.trim() || !productId || !secondsValid}
            >
              {isSaving ? "Criando" : "Criar pedido"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
