import { CommonModule } from '@angular/common';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription, catchError, finalize, forkJoin, of } from 'rxjs';

interface Produto {
  id: number;
  codigo: string;
  descricao: string;
  saldo: number;
}

interface NotaItem {
  produtoCodigo: string;
  quantidade: number;
}

interface NotaFiscal {
  id: number;
  numero: number;
  status: 'Aberta' | 'Fechada';
  criadoEm: string;
  impressoEm?: string;
  itens: NotaItem[];
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly estoqueUrl = 'http://localhost:5001';
  private readonly faturamentoUrl = 'http://localhost:5002';
  private readonly subscriptions = new Subscription();

  produtos: Produto[] = [];
  notas: NotaFiscal[] = [];
  mensagem = '';
  erro = '';
  carregando = false;
  imprimindoId?: number;
  simularFalha = false;

  produtoForm = {
    codigo: '',
    descricao: '',
    saldo: 0
  };

  notaItens: NotaItem[] = [
    { produtoCodigo: '', quantidade: 1 }
  ];

  constructor(private readonly http: HttpClient) {}

  ngOnInit(): void {
    this.carregarDados();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  carregarDados(): void {
    this.carregando = true;
    this.erro = '';

    const sub = forkJoin({
      produtos: this.http.get<Produto[]>(`${this.estoqueUrl}/produtos`),
      notas: this.http.get<NotaFiscal[]>(`${this.faturamentoUrl}/notas`)
    }).pipe(
      catchError((error) => {
        this.erro = this.extrairErro(error);
        return of({ produtos: [], notas: [] });
      }),
      finalize(() => this.carregando = false)
    ).subscribe(({ produtos, notas }) => {
      this.produtos = produtos;
      this.notas = notas;
    });

    this.subscriptions.add(sub);
  }

  cadastrarProduto(): void {
    this.limparMensagens();

    const sub = this.http.post<Produto>(`${this.estoqueUrl}/produtos`, this.produtoForm).pipe(
      catchError((error) => {
        this.erro = this.extrairErro(error);
        return of(null);
      })
    ).subscribe((produto) => {
      if (!produto) return;
      this.mensagem = 'Produto cadastrado.';
      this.produtoForm = { codigo: '', descricao: '', saldo: 0 };
      this.carregarDados();
    });

    this.subscriptions.add(sub);
  }

  adicionarItem(): void {
    this.notaItens.push({ produtoCodigo: '', quantidade: 1 });
  }

  removerItem(index: number): void {
    this.notaItens.splice(index, 1);
    if (this.notaItens.length === 0) {
      this.adicionarItem();
    }
  }

  criarNota(): void {
    this.limparMensagens();
    const itens = this.notaItens.filter(item => item.produtoCodigo && item.quantidade > 0);

    const sub = this.http.post<NotaFiscal>(`${this.faturamentoUrl}/notas`, { itens }).pipe(
      catchError((error) => {
        this.erro = this.extrairErro(error);
        return of(null);
      })
    ).subscribe((nota) => {
      if (!nota) return;
      this.mensagem = `Nota ${nota.numero} criada.`;
      this.notaItens = [{ produtoCodigo: '', quantidade: 1 }];
      this.carregarDados();
    });

    this.subscriptions.add(sub);
  }

  imprimir(nota: NotaFiscal): void {
    this.limparMensagens();
    this.imprimindoId = nota.id;

    const sub = this.http.post<NotaFiscal>(`${this.faturamentoUrl}/notas/${nota.id}/imprimir`, {
      simularFalha: this.simularFalha
    }).pipe(
      catchError((error) => {
        this.erro = this.extrairErro(error);
        return of(null);
      }),
      finalize(() => this.imprimindoId = undefined)
    ).subscribe((notaAtualizada) => {
      if (!notaAtualizada) return;
      this.mensagem = `Nota ${notaAtualizada.numero} impressa e fechada.`;
      this.carregarDados();
    });

    this.subscriptions.add(sub);
  }

  private limparMensagens(): void {
    this.mensagem = '';
    this.erro = '';
  }

  private extrairErro(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (typeof error.error === 'string' && error.error.trim()) return error.error;
      if (error.error?.detail) return error.error.detail;
      if (error.error?.title) return error.error.title;
      return `Erro ${error.status || ''} ao comunicar com a API.`;
    }

    return 'Erro inesperado.';
  }
}
