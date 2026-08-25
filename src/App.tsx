import { useReducer, useEffect } from 'react';
import { evaluate } from './parser';
import './App.css';

// ── State ───────────────────────────────────────────────────────────────────

interface CalcState {
  expr: string;
  equationLine: string;
  resultLine: string;
  errorMsg: string;
  history: string[];
  useDegrees: boolean;
  justCalc: boolean;
}

type Action =
  | { type: 'INPUT'; val: string }
  | { type: 'TOGGLE_DEG' }
  | { type: 'LOAD_HISTORY'; entry: string }
  | { type: 'CLEAR_HISTORY' };

function formatNum(n: number): string {
  if (isNaN(n)) return 'Не число';
  if (!isFinite(n)) return '∞';
  const r = Math.round(n * 1e10) / 1e10;
  if (Number.isInteger(r) && Math.abs(r) < 1e15) return r.toString();
  return parseFloat(r.toPrecision(10)).toString();
}

function getLastToken(expr: string): string {
  if (!expr) return '0';
  const m = expr.match(/([\d.]+)$/);
  return m ? m[1] : '';
}

function isOp(val: string) {
  return val === '+' || val === '−' || val === '×' || val === '÷';
}

function appendToExpr(expr: string, val: string): string {
  switch (val) {
    case 'sin':  return expr + 'sin(';
    case 'cos':  return expr + 'cos(';
    case 'tan':  return expr + 'tan(';
    case 'sqrt': return expr + 'sqrt(';
    case 'x²':   return expr + '^2';
    case 'xʸ':   return expr + '^';
    case 'π':    return expr + 'π';
    default:     return expr + val;
  }
}

function backspace(expr: string): string {
  const fns = ['sin(', 'cos(', 'tan(', 'sqrt('];
  for (const fn of fns) if (expr.endsWith(fn)) return expr.slice(0, -fn.length);
  return expr.slice(0, -1);
}

function removeLastToken(expr: string): string {
  const fns = ['sin(', 'cos(', 'tan(', 'sqrt('];
  for (const fn of fns) if (expr.endsWith(fn)) return expr.slice(0, -fn.length);
  const m = expr.match(/^(.*?)([\d.]+)$/);
  if (m) return m[1];
  return expr.slice(0, -1);
}

function toggleSign(expr: string): string {
  const m = expr.match(/^(.*?)([\d.]+)$/);
  if (!m) return expr;
  const [, before, num] = m;
  if (before.endsWith('−')) return before.slice(0, -1) + num;
  return before + '−' + num;
}

function makeDisplay(expr: string): string {
  const tok = getLastToken(expr);
  return tok || expr || '0';
}

function reducer(state: CalcState, action: Action): CalcState {
  switch (action.type) {
    case 'TOGGLE_DEG':
      return { ...state, useDegrees: !state.useDegrees };

    case 'CLEAR_HISTORY':
      return { ...state, history: [] };

    case 'LOAD_HISTORY': {
      const idx = action.entry.lastIndexOf(' = ');
      const e = idx >= 0 ? action.entry.slice(0, idx) : action.entry;
      return {
        ...state,
        expr: e,
        equationLine: e,
        resultLine: makeDisplay(e),
        errorMsg: '',
        justCalc: false,
      };
    }

    case 'INPUT': {
      const { val } = action;
      let { expr, justCalc } = state;

      // ── = ──────────────────────────────────────────────
      if (val === '=') {
        if (!expr) return state;
        try {
          const res = evaluate(expr, state.useDegrees);
          const resStr = formatNum(res);
          const entry = `${expr} = ${resStr}`;
          return {
            ...state,
            expr: resStr,
            equationLine: `${expr} =`,
            resultLine: resStr,
            errorMsg: '',
            history: [entry, ...state.history.slice(0, 99)],
            justCalc: true,
          };
        } catch (err: unknown) {
          const msg = err instanceof Error ? err.message : 'Помилка';
          return { ...state, errorMsg: msg, resultLine: 'Помилка', equationLine: expr };
        }
      }

      // ── After = ────────────────────────────────────────
      if (justCalc && val !== 'CE' && val !== '⌫' && val !== 'C') {
        if (!isOp(val)) expr = ''; // start fresh for non-operator
        justCalc = false;
      }

      // ── Modify expression ──────────────────────────────
      let next = expr;
      switch (val) {
        case 'C':   next = ''; break;
        case 'CE':  next = removeLastToken(expr); break;
        case '⌫':   next = backspace(expr); break;
        case '+/−': next = toggleSign(expr); break;
        default:    next = appendToExpr(expr, val); break;
      }

      return {
        ...state,
        expr: next,
        equationLine: next,
        resultLine: makeDisplay(next),
        errorMsg: '',
        justCalc,
      };
    }

    default:
      return state;
  }
}

const initial: CalcState = {
  expr: '',
  equationLine: '',
  resultLine: '0',
  errorMsg: '',
  history: [],
  useDegrees: true,
  justCalc: false,
};

// ── Button layout ───────────────────────────────────────────────────────────

type BtnKind = 'digit' | 'op' | 'func' | 'paren' | 'clear' | 'equals' | 'const';
interface BtnDef { label: string; tag: string; kind: BtnKind; tall?: true }

const ROWS: BtnDef[][] = [
  [
    { label: 'sin', tag: 'sin',  kind: 'func'  },
    { label: 'cos', tag: 'cos',  kind: 'func'  },
    { label: 'tan', tag: 'tan',  kind: 'func'  },
    { label: '√x',  tag: 'sqrt', kind: 'func'  },
    { label: 'π',   tag: 'π',   kind: 'const' },
  ],
  [
    { label: 'x²',  tag: 'x²',  kind: 'func'  },
    { label: 'xʸ',  tag: 'xʸ',  kind: 'func'  },
    { label: '(',   tag: '(',   kind: 'paren' },
    { label: ')',   tag: ')',   kind: 'paren' },
    { label: 'C',   tag: 'C',   kind: 'clear' },
  ],
  [
    { label: '7', tag: '7', kind: 'digit' },
    { label: '8', tag: '8', kind: 'digit' },
    { label: '9', tag: '9', kind: 'digit' },
    { label: '÷', tag: '÷', kind: 'op'    },
    { label: '⌫', tag: '⌫', kind: 'clear' },
  ],
  [
    { label: '4',  tag: '4',  kind: 'digit' },
    { label: '5',  tag: '5',  kind: 'digit' },
    { label: '6',  tag: '6',  kind: 'digit' },
    { label: '×',  tag: '×',  kind: 'op'    },
    { label: 'CE', tag: 'CE', kind: 'clear' },
  ],
  [
    { label: '1', tag: '1', kind: 'digit' },
    { label: '2', tag: '2', kind: 'digit' },
    { label: '3', tag: '3', kind: 'digit' },
    { label: '−', tag: '−', kind: 'op'   },
    { label: '=', tag: '=', kind: 'equals', tall: true },
  ],
  [
    { label: '+/−', tag: '+/−', kind: 'func'  },
    { label: '0',   tag: '0',   kind: 'digit' },
    { label: '.',   tag: '.',   kind: 'digit' },
    { label: '+',   tag: '+',   kind: 'op'    },
    // = spans rows 4-5, rendered via CSS grid-row
  ],
];

// ── Component ───────────────────────────────────────────────────────────────

export default function App() {
  const [state, dispatch] = useReducer(reducer, initial);
  const { equationLine, resultLine, errorMsg, history, useDegrees } = state;

  const send = (val: string) => dispatch({ type: 'INPUT', val });

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      const t = e.target as HTMLElement;
      if (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA') return;
      if (!e.shiftKey && e.key >= '0' && e.key <= '9') { send(e.key); e.preventDefault(); return; }
      const map: Record<string, string> = {
        '.': '.', ',': '.', '+': '+', '-': '−', '*': '×', '/': '÷',
        '^': 'xʸ', '(': '(', ')': ')', 'Enter': '=', '=': '=',
        'Backspace': '⌫', 'Escape': 'C', 'Delete': 'CE',
      };
      if (e.shiftKey && e.key === '8') { send('×'); e.preventDefault(); return; }
      if (map[e.key]) { send(map[e.key]); e.preventDefault(); }
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, []); // stable: send is dispatch-bound, never changes

  return (
    <div className="app">
      {/* ── Calculator ───────────────────────────── */}
      <div className="calc-panel">
        <div className="display">
          <div
            className="deg-toggle"
            onClick={() => dispatch({ type: 'TOGGLE_DEG' })}
            title="Перемикач градуси/радіани"
          >
            {useDegrees ? 'DEG' : 'RAD'}
          </div>
          <div className="equation-line" title={equationLine}>{equationLine || ' '}</div>
          <div className={`result-line${errorMsg ? ' error' : ''}`}>
            {errorMsg || resultLine}
          </div>
        </div>

        <div className="btn-grid">
          {ROWS.map((row, ri) =>
            row.map(btn => (
              <button
                key={`${ri}-${btn.tag}`}
                className={`btn btn-${btn.kind}${btn.tall ? ' btn-tall' : ''}`}
                onClick={() => send(btn.tag)}
              >
                {btn.label}
              </button>
            ))
          )}
        </div>
      </div>

      {/* ── History ──────────────────────────────── */}
      <div className="history-panel">
        <div className="history-header">
          <span>Історія</span>
          <button className="clear-hist" onClick={() => dispatch({ type: 'CLEAR_HISTORY' })}>
            Очистити
          </button>
        </div>
        <div className="history-list">
          {history.length === 0 && (
            <div className="history-empty">Тут з'являться обчислення</div>
          )}
          {history.map((entry, i) => (
            <div
              key={i}
              className="history-item"
              onClick={() => dispatch({ type: 'LOAD_HISTORY', entry })}
              title="Натисни щоб завантажити"
            >
              {entry}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
