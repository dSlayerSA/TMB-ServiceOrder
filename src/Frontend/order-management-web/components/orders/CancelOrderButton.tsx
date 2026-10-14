"use client";

import { useState } from "react";
import { AlertCircle, Ban } from "lucide-react";
import { ApiError, cancelOrder } from "@/lib/api";
import type { OrderDetail } from "@/types/order";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogBody,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { StageMark } from "@/components/orders/StageMark";

/**
 * Cancels an order that has not settled yet. The interesting case is an order
 * still Pendente: the worker has not touched it, and may not even have received
 * the message yet. Whatever is in flight finds a terminal order and stops.
 */
export function CancelOrderButton({
  order,
  onCancelled,
}: {
  order: OrderDetail;
  /** Receives the cancelled order so the view can update without a refetch. */
  onCancelled: (cancelled: OrderDetail) => void;
}) {
  const [open, setOpen] = useState(false);
  const [isCancelling, setIsCancelling] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const confirm = async () => {
    if (isCancelling) return;

    setError(null);
    setIsCancelling(true);
    try {
      const cancelled = await cancelOrder(order.id);
      setOpen(false);
      onCancelled(cancelled);
    } catch (caught) {
      // 409 is the order settling between opening this dialog and confirming -
      // the worker got there first. That is a state conflict, not a failure.
      setError(
        caught instanceof ApiError && caught.status === 409
          ? "O pedido foi encerrado enquanto esta janela estava aberta."
          : "O cancelamento não foi aplicado. Verifique a conexão e tente de novo."
      );
    } finally {
      setIsCancelling(false);
    }
  };

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        if (!next) setError(null);
      }}
    >
      <DialogTrigger
        render={
          <Button variant="outline" size="sm">
            <Ban className="size-3.5" />
            Cancelar pedido
          </Button>
        }
      />

      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>Cancelar este pedido?</DialogTitle>
          <DialogDescription>
            {order.status === "Pendente"
              ? "O worker ainda não começou este pedido."
              : "O worker está processando este pedido agora."}
          </DialogDescription>
        </DialogHeader>

        <DialogBody className="space-y-4">
          <dl className="space-y-1.5 text-sm">
            <div className="flex items-baseline justify-between gap-4">
              <dt className="label">Pedido</dt>
              <dd className="truncate">{order.product}</dd>
            </div>
            <div className="flex items-baseline justify-between gap-4">
              <dt className="label">Cliente</dt>
              <dd className="truncate">{order.customer}</dd>
            </div>
            <div className="flex items-baseline justify-between gap-4">
              <dt className="label">Passa a</dt>
              <dd className="flex items-center gap-1.5 text-stage-cancelled">
                <StageMark status="Cancelado" className="size-2.5" />
                Cancelado
              </dd>
            </div>
          </dl>

          <p className="text-sm text-muted-foreground">
            Cancelado é um estado final: o pedido não volta atrás e não pode ser
            retomado.
          </p>

          {error && (
            <p className="flex items-start gap-2 text-sm text-destructive">
              <AlertCircle className="mt-0.5 size-4 shrink-0" />
              {error}
            </p>
          )}
        </DialogBody>

        <DialogFooter>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => setOpen(false)}
            disabled={isCancelling}
          >
            Voltar
          </Button>
          <Button type="button" variant="destructive" size="sm" onClick={confirm} disabled={isCancelling}>
            {isCancelling ? "Cancelando" : "Cancelar pedido"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
