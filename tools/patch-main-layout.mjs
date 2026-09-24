import fs from "fs";

const root = "d:/PressureTestApp";
const axPath = `${root}/src/PressureTestApp.UI/Views/MainWindow.axaml`;
const pyPath = `${root}/tools/relayout_main_window.py`;

let text = fs.readFileSync(axPath, "utf8");
const py = fs.readFileSync(pyPath, "utf8");
const a = py.indexOf('NEW = """') + 9;
const b = py.indexOf('\n""".replace("__SIGNALS__"', a);
if (a < 9 || b < 0) throw new Error("Could not extract NEW template from py");
let NEW = py.slice(a, b);

const s1 =
  "      <!-- ▼ 行 1：顶栏 — 左：检测区+信息录入；右：混合模拟+试验时间 | 任务明细 | 回路+手动设置 -->";
const sOp = "      <!-- 操作栏：固定在内容区底部 -->";
const iStart = text.indexOf(s1);
const iOp = text.indexOf(sOp);
const iContentClose = text.indexOf("    </Grid>", iOp);

const sigStart = text.indexOf("      <!-- ▼ 行 2：检测信号灯 -->");
const sigEnd = text.indexOf("      <!-- ▼ 行 3：耐压曲线", sigStart);
const rawSig = text.slice(sigStart, sigEnd).replace(/\n+$/, "");
const signalsBlock = rawSig
  .split("\n")
  .map((line) =>
    line.startsWith("      ") ? "          " + line.slice(6) : "          " + line
  )
  .join("\n")
  .replace('Grid.Row="2"', 'Grid.Row="0"');

const cStart = text.indexOf("      <!-- ▼ 行 3：耐压曲线");
const chartBlockStart = text.indexOf('          <Border Grid.Row="1" Grid.Column="0"', cStart);
const chartBlockEnd =
  text.indexOf("            </lvc:CartesianChart>", chartBlockStart) +
  "            </lvc:CartesianChart>".length;
const chartRaw = text.slice(chartBlockStart, chartBlockEnd);
const chartBlock = chartRaw
  .split("\n")
  .map((line) =>
    line.startsWith("          ") ? "              " + line.slice(10) : "              " + line
  )
  .join("\n");

NEW = NEW.replace("__SIGNALS__", signalsBlock).replace("__CHART__", chartBlock);

text = text.slice(0, iStart) + NEW + "\n      </Grid>\n" + text.slice(iContentClose);
fs.writeFileSync(axPath, text, "utf8");
console.log("patched", axPath);
