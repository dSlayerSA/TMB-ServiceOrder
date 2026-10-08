"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { createOrder } from "@/lib/api";

const schema = z.object({
  customer: z.string().trim().min(1, "Informe o nome do cliente").max(200),
  product: z.string().trim().min(1, "Informe o produto").max(200),
  amount: z.coerce.number().positive("O valor deve ser maior que zero"),
});

type FormInput = z.input<typeof schema>;
type FormOutput = z.output<typeof schema>;

export function OrderForm() {
  const router = useRouter();
  const [submitError, setSubmitError] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormInput, unknown, FormOutput>({ resolver: zodResolver(schema) });

  const onSubmit = async (values: FormOutput) => {
    setSubmitError(null);
    try {
      const order = await createOrder(values);
      router.push(`/orders/${order.id}`);
    } catch {
      setSubmitError("Não foi possível criar o pedido. Tente novamente.");
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
      <Field label="Cliente" error={errors.customer?.message}>
        <input
          {...register("customer")}
          type="text"
          placeholder="Maria Silva"
          className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-900 focus:outline-none dark:border-gray-700 dark:bg-gray-900 dark:focus:border-gray-100"
        />
      </Field>

      <Field label="Produto" error={errors.product?.message}>
        <input
          {...register("product")}
          type="text"
          placeholder="Notebook"
          className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-900 focus:outline-none dark:border-gray-700 dark:bg-gray-900 dark:focus:border-gray-100"
        />
      </Field>

      <Field label="Valor (R$)" error={errors.amount?.message}>
        <input
          {...register("amount")}
          type="number"
          step="0.01"
          min="0.01"
          placeholder="3500.00"
          className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-gray-900 focus:outline-none dark:border-gray-700 dark:bg-gray-900 dark:focus:border-gray-100"
        />
      </Field>

      {submitError && <p className="text-sm text-red-600 dark:text-red-400">{submitError}</p>}

      <button
        type="submit"
        disabled={isSubmitting}
        className="w-full rounded-md bg-gray-900 px-4 py-2 text-sm font-medium text-white hover:bg-gray-700 disabled:opacity-50 dark:bg-gray-100 dark:text-gray-900 dark:hover:bg-white"
      >
        {isSubmitting ? "Criando..." : "Criar pedido"}
      </button>
    </form>
  );
}

function Field({ label, error, children }: { label: string; error?: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-sm font-medium text-gray-700 dark:text-gray-300">{label}</span>
      {children}
      {error && <span className="mt-1 block text-xs text-red-600 dark:text-red-400">{error}</span>}
    </label>
  );
}
