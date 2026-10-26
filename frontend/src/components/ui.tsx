"use client";

import type { ReactNode } from "react";
import type { ApiError } from "@/lib/api/http";
import type { PagedResult } from "@/lib/api/types";

// Small, unstyled-ish building blocks. The UI is not evaluated; clarity over looks.

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: string; actions?: ReactNode }) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 className="text-2xl font-semibold">{title}</h1>
        {subtitle && <p className="text-sm text-gray-600">{subtitle}</p>}
      </div>
      {actions}
    </div>
  );
}

export function ErrorAlert({ error }: { error: ApiError | null }) {
  if (!error) return null;
  return (
    <div role="alert" className="mb-4 rounded border border-red-300 bg-red-50 p-3 text-sm text-red-800">
      <p className="font-medium">{error.detail}</p>
      <p className="mt-1 text-xs text-red-700">
        Código: {error.code}
        {error.status ? ` · HTTP ${error.status}` : ""}
        {error.correlationId ? ` · Correlación: ${error.correlationId}` : ""}
      </p>
    </div>
  );
}

export function Loading({ label = "Cargando..." }: { label?: string }) {
  return <p className="py-6 text-sm text-gray-500">{label}</p>;
}

export function EmptyState({ label }: { label: string }) {
  return <p className="py-6 text-sm text-gray-500">{label}</p>;
}

export function Pagination({ page, onPageChange }: { page: PagedResult<unknown>; onPageChange: (pageNumber: number) => void }) {
  return (
    <div className="mt-4 flex items-center justify-between text-sm">
      <span>
        {page.totalRecords} registro{page.totalRecords === 1 ? "" : "s"} · página {page.pageNumber} de {Math.max(page.totalPages, 1)}
      </span>
      <div className="flex gap-2">
        <button type="button" className="btn" disabled={!page.hasPreviousPage} onClick={() => onPageChange(page.pageNumber - 1)}>
          Anterior
        </button>
        <button type="button" className="btn" disabled={!page.hasNextPage} onClick={() => onPageChange(page.pageNumber + 1)}>
          Siguiente
        </button>
      </div>
    </div>
  );
}

export function Field({
  label,
  error,
  children,
}: {
  label: string;
  error?: string;
  children: ReactNode;
}) {
  return (
    <label className="block text-sm">
      <span className="mb-1 block font-medium">{label}</span>
      {children}
      {error && <span className="mt-1 block text-xs text-red-700">{error}</span>}
    </label>
  );
}

export function StatusBadge({ status }: { status: string }) {
  const labels: Record<string, string> = { Scheduled: "Programado", Finished: "Finalizado", Cancelled: "Cancelado" };
  const colors: Record<string, string> = {
    Scheduled: "bg-blue-100 text-blue-800",
    Finished: "bg-green-100 text-green-800",
    Cancelled: "bg-gray-200 text-gray-700",
  };
  return <span className={`rounded px-2 py-0.5 text-xs ${colors[status] ?? ""}`}>{labels[status] ?? status}</span>;
}

export function Table({ children }: { children: ReactNode }) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse text-sm">{children}</table>
    </div>
  );
}
