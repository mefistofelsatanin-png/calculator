// Recursive descent expression parser
// Handles: +, -, *, /, ^, sin, cos, tan, sqrt, abs, log, ln, π, e
// Operator precedence: () > ^ > * / > + -

export function evaluate(expr: string, useDegrees: boolean): number {
  const normalized = expr
    .replace(/×/g, '*')
    .replace(/÷/g, '/')
    .replace(/−/g, '-')
    .replace(/π/g, Math.PI.toString())
    .replace(/\be\b/g, Math.E.toString());

  const parser = new Parser(normalized, useDegrees);
  const result = parser.parse();
  return result;
}

class Parser {
  private s: string;
  private p: number;
  private deg: boolean;

  constructor(input: string, degrees: boolean) {
    this.s = input.trim();
    this.p = 0;
    this.deg = degrees;
  }

  parse(): number {
    const v = this.parseExpr();
    this.skip();
    if (this.p < this.s.length) {
      throw new Error(`Неочікуваний символ: "${this.s[this.p]}"`);
    }
    return v;
  }

  private parseExpr(): number {
    let left = this.parseTerm();
    while (this.p < this.s.length) {
      this.skip();
      if (this.s[this.p] === '+') { this.p++; left += this.parseTerm(); }
      else if (this.s[this.p] === '-') { this.p++; left -= this.parseTerm(); }
      else break;
    }
    return left;
  }

  private parseTerm(): number {
    let left = this.parsePower();
    while (this.p < this.s.length) {
      this.skip();
      if (this.s[this.p] === '*') { this.p++; left *= this.parsePower(); }
      else if (this.s[this.p] === '/') {
        this.p++;
        const d = this.parsePower();
        if (d === 0) throw new Error('Ділення на нуль');
        left /= d;
      }
      else break;
    }
    return left;
  }

  // Right-associative
  private parsePower(): number {
    const base = this.parseUnary();
    this.skip();
    if (this.p < this.s.length && this.s[this.p] === '^') {
      this.p++;
      const exp = this.parsePower();
      return Math.pow(base, exp);
    }
    return base;
  }

  private parseUnary(): number {
    this.skip();
    if (this.s[this.p] === '-') { this.p++; return -this.parseUnary(); }
    if (this.s[this.p] === '+') { this.p++; return this.parseUnary(); }
    return this.parseAtom();
  }

  private parseAtom(): number {
    this.skip();
    if (this.p >= this.s.length) throw new Error('Очікувалось число або вираз');

    const fns = ['sqrt', 'sin', 'cos', 'tan', 'abs', 'log', 'ln'];
    for (const fn of fns) {
      if (this.s.substring(this.p).startsWith(fn)) {
        this.p += fn.length;
        this.skip();
        if (this.s[this.p] !== '(') throw new Error(`Очікувалась ( після ${fn}`);
        this.p++;
        const arg = this.parseExpr();
        this.skip();
        if (this.s[this.p] !== ')') throw new Error('Очікувалась )');
        this.p++;
        return this.applyFn(fn, arg);
      }
    }

    if (this.s[this.p] === '(') {
      this.p++;
      const v = this.parseExpr();
      this.skip();
      if (this.s[this.p] !== ')') throw new Error('Очікувалась )');
      this.p++;
      return v;
    }

    return this.parseNumber();
  }

  private applyFn(fn: string, arg: number): number {
    const rad = this.deg ? (arg * Math.PI) / 180 : arg;
    switch (fn) {
      case 'sqrt':
        if (arg < 0) throw new Error("Корінь з від'ємного числа");
        return Math.sqrt(arg);
      case 'sin': return +Math.sin(rad).toFixed(10);
      case 'cos': return +Math.cos(rad).toFixed(10);
      case 'tan': {
        const t = Math.tan(rad);
        if (!isFinite(t)) throw new Error('tan невизначений');
        return +t.toFixed(10);
      }
      case 'abs': return Math.abs(arg);
      case 'log': return Math.log10(arg);
      case 'ln':  return Math.log(arg);
      default: return arg;
    }
  }

  private parseNumber(): number {
    this.skip();
    const start = this.p;
    let hasDot = false;
    while (this.p < this.s.length) {
      const ch = this.s[this.p];
      if (/\d/.test(ch)) { this.p++; }
      else if (ch === '.' && !hasDot) { hasDot = true; this.p++; }
      else break;
    }
    if (this.p === start) throw new Error(`Очікувалось число на позиції ${this.p}`);
    return parseFloat(this.s.slice(start, this.p));
  }

  private skip() {
    while (this.p < this.s.length && this.s[this.p] === ' ') this.p++;
  }
}
