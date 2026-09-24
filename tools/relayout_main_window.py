# -*- coding: utf-8 -*-
"""Replace MainWindow.axaml main-test block with 3-column layout (Fig.1 style)."""
from pathlib import Path

path = Path(__file__).resolve().parent.parent / "src" / "PressureTestApp.UI" / "Views" / "MainWindow.axaml"
text = path.read_text(encoding="utf-8")

s1 = "      <!-- ▼ 行 1：顶栏 — 左：检测区+信息录入；右：混合模拟+试验时间 | 任务明细 | 回路+手动设置 -->"
s_op = "      <!-- 操作栏：固定在内容区底部 -->"

i_start = text.index(s1)
i_op = text.index(s_op)
i_content_close = text.index("    </Grid>", i_op)

sig_start = text.index("      <!-- ▼ 行 2：检测信号灯 -->")
sig_end = text.index("      <!-- ▼ 行 3：耐压曲线", sig_start)
raw_sig = text[sig_start:sig_end].rstrip("\n")
sig_lines = []
for line in raw_sig.split("\n"):
    if line.startswith("      "):
        sig_lines.append("          " + line[6:])
    else:
        sig_lines.append("          " + line)
signals_block = "\n".join(sig_lines).replace('Grid.Row="2"', 'Grid.Row="0"')

c_start = text.index("      <!-- ▼ 行 3：耐压曲线")
chart_block_start = text.index('          <Border Grid.Row="1" Grid.Column="0"', c_start)
chart_block_end = text.index("            </lvc:CartesianChart>", chart_block_start) + len(
    "            </lvc:CartesianChart>"
)
chart_raw = text[chart_block_start:chart_block_end]
chart_lines = []
for line in chart_raw.split("\n"):
    chart_lines.append("              " + line[10:] if line.startswith("          ") else "              " + line)
chart_block = "\n".join(chart_lines)

# XAML fragments: use .format with placeholder {{ for single { in output
NEW = """      <Grid Grid.Row="1" ColumnDefinitions="264,*,220" MinHeight="0">
        <ScrollViewer Grid.Column="0" HorizontalScrollBarVisibility="Disabled"
                      VerticalScrollBarVisibility="Auto" Margin="0,0,10,0">
          <StackPanel Spacing="8">
            <Border Background="{StaticResource BrushBgPanel}" CornerRadius="8"
                    BorderBrush="{StaticResource BrushBorder}" BorderThickness="1" Padding="8,8">
              <StackPanel Spacing="6">
                <TextBlock Text="{Binding ModeHint}" FontSize="10" TextWrapping="Wrap"
                           Foreground="{StaticResource BrushTextTertiary}"/>
                <Border Background="{StaticResource BrushBgWindow}" CornerRadius="8"
                        BorderBrush="{StaticResource BrushBorder}" BorderThickness="1" Padding="4">
                  <StackPanel Orientation="Horizontal" Spacing="4">
                    <Button Classes="mode-tab" Classes.active-online="{Binding IsOnlineMode}"
                            Content="在线模式" MinWidth="72"
                            Command="{Binding SwitchModeCommand}" CommandParameter="online"/>
                    <Button Classes="mode-tab" Classes.active-offline="{Binding IsOfflineMode}"
                            Content="离线模式" MinWidth="72"
                            Command="{Binding SwitchModeCommand}" CommandParameter="offline"/>
                    <Button Classes="mode-tab" Classes.active-import="{Binding IsExcelImportMode}"
                            Content="Excel" MinWidth="64"
                            Command="{Binding SwitchModeCommand}" CommandParameter="excel"/>
                  </StackPanel>
                </Border>
                <TextBlock Text="{Binding NetIndicatorText}" FontSize="10"
                           Foreground="{StaticResource BrushInfo}" TextWrapping="Wrap"/>
              </StackPanel>
            </Border>

            <Border Background="{StaticResource BrushBgPanel}" CornerRadius="8"
                    BorderBrush="{StaticResource BrushBorder}" BorderThickness="1" ClipToBounds="True">
              <Grid RowDefinitions="Auto,Auto,Auto,*">
                <Border Grid.Row="0" Background="{StaticResource BrushPrimary}" Padding="8,6">
                  <TextBlock Text="信息录入" Foreground="White" FontSize="12" FontWeight="SemiBold"/>
                </Border>
                <Border Grid.Row="1" Background="#FF353942" Padding="6,4">
                  <TextBlock Text="车型选择" FontSize="11" Foreground="{StaticResource BrushTextSecondary}"
                             HorizontalAlignment="Center"/>
                </Border>
                <Grid Grid.Row="2" Margin="8,6,8,4" ColumnDefinitions="Auto,*" RowDefinitions="Auto,Auto">
                  <TextBlock Grid.Row="0" Grid.Column="0" Text="车型型号" FontSize="11" VerticalAlignment="Center"
                             Margin="0,0,8,6" Foreground="{StaticResource BrushTextTertiary}"/>
                  <TextBox Grid.Row="0" Grid.Column="1" Text="{Binding InfoEntryCarTypeText}"
                           Watermark="请选择" MinHeight="32" FontSize="11" Margin="0,0,0,6"
                           Background="{StaticResource BrushBgInput}" BorderBrush="{StaticResource BrushBorder}"/>
                  <TextBlock Grid.Row="1" Grid.Column="0" Text="车型车" FontSize="11" VerticalAlignment="Center"
                             Margin="0,0,8,0" Foreground="{StaticResource BrushTextTertiary}"/>
                  <TextBox Grid.Row="1" Grid.Column="1" Text="{Binding InfoEntryTrainCarText}"
                           Watermark="请输入" MinHeight="32" FontSize="11"
                           Background="{StaticResource BrushBgInput}" BorderBrush="{StaticResource BrushBorder}"/>
                </Grid>
                <Button Grid.Row="3" Margin="8,0,8,8" Content="车型选择…" FontSize="11"
                        Command="{Binding InfoEntrySelectVehicleModelCommand}"
                        HorizontalAlignment="Stretch" Padding="8,6"
                        Background="{StaticResource BrushBgInput}" BorderBrush="{StaticResource BrushBorder}"/>
              </Grid>
            </Border>

            <Border Background="{StaticResource BrushBgPanel}" CornerRadius="8"
                    BorderBrush="{StaticResource BrushBorder}" BorderThickness="1" ClipToBounds="True">
              <StackPanel Spacing="6">
                <Border Background="{StaticResource BrushPrimary}" Padding="8,6">
                  <TextBlock Text="参数设定" Foreground="White" FontSize="12" FontWeight="SemiBold"/>
                </Border>
                <UniformGrid Columns="2" Rows="2" Margin="8,0,8,8">
                  <RadioButton GroupName="ParamLoop" Content="回路1" Margin="4"
                               IsChecked="{Binding IsParameterLoop1, Mode=TwoWay}"/>
                  <RadioButton GroupName="ParamLoop" Content="回路2" Margin="4"
                               IsChecked="{Binding IsParameterLoop2, Mode=TwoWay}"/>
                  <RadioButton GroupName="ParamLoop" Content="回路3" Margin="4"
                               IsChecked="{Binding IsParameterLoop3, Mode=TwoWay}"/>
                  <RadioButton GroupName="ParamLoop" Content="回路4" Margin="4"
                               IsChecked="{Binding IsParameterLoop4, Mode=TwoWay}"/>
                </UniformGrid>
                <StackPanel Margin="8,0,8,8" Spacing="4" IsVisible="{Binding ShowLoopCards}">
                  <ItemsControl ItemsSource="{Binding Loops}">
                    <ItemsControl.ItemTemplate>
                      <DataTemplate x:DataType="vm:LoopCardViewModel">
                        <Border Background="#0FEF9A4E" Margin="0,0,0,4" Padding="6,4" CornerRadius="6"
                                BorderBrush="{StaticResource BrushPrimary}" BorderThickness="1">
                          <Grid ColumnDefinitions="Auto,*">
                            <TextBlock Text="{Binding Title}" FontSize="10" FontWeight="SemiBold"
                                       Foreground="{StaticResource BrushPrimary}" VerticalAlignment="Center"/>
                            <TextBlock Grid.Column="1" Text="{Binding MetaText}" FontSize="9"
                                       Foreground="{StaticResource BrushTextTertiary}" TextTrimming="CharacterEllipsis"
                                       Margin="6,0,0,0" VerticalAlignment="Center"/>
                          </Grid>
                        </Border>
                      </DataTemplate>
                    </ItemsControl.ItemTemplate>
                  </ItemsControl>
                </StackPanel>
              </StackPanel>
            </Border>

            <Border Background="{StaticResource BrushBgPanel}" CornerRadius="8"
                    BorderBrush="{StaticResource BrushBorder}" BorderThickness="1"
                    IsVisible="{Binding ShowTaskNavigator}">
              <Grid RowDefinitions="Auto,*" MinHeight="120">
                <Border Grid.Row="0" Padding="8,6" BorderBrush="{StaticResource BrushBorder}" BorderThickness="0,0,0,1">
                  <Grid ColumnDefinitions="Auto,*,Auto">
                    <TextBlock Text="任务明细" FontSize="11" FontWeight="SemiBold"
                               Foreground="{StaticResource BrushTextPrimary}" VerticalAlignment="Center"/>
                    <TextBlock Grid.Column="1" Text="{Binding CurrentTaskTitle}" FontSize="11" Margin="8,0"
                               TextTrimming="CharacterEllipsis" VerticalAlignment="Center"/>
                    <StackPanel Grid.Column="2" Orientation="Horizontal" Spacing="4">
                      <Button Content="‹" Padding="6,2" FontSize="11" IsEnabled="{Binding HasPrevious}"
                              Command="{Binding PreviousDetailCommand}"/>
                      <Button Content="›" Padding="6,2" FontSize="11" IsEnabled="{Binding HasNext}"
                              Command="{Binding NextDetailCommand}"/>
                    </StackPanel>
                  </Grid>
                </Border>
                <Border Grid.Row="1" Background="{StaticResource BrushBgWindow}" Padding="8,6">
                  <StackPanel Spacing="4">
                    <TextBlock Text="{Binding CurrentDetailDisplay}" FontSize="10"
                               Foreground="{StaticResource BrushTextTertiary}"/>
                    <TextBlock FontSize="10" Foreground="{StaticResource BrushTextTertiary}" TextWrapping="Wrap">
                      <Run Text="车型 "/><Run Text="{Binding CurrentCarType}"/>
                      <Run Text=" · 列辆 "/><Run Text="{Binding CurrentTrainCar}"/>
                    </TextBlock>
                  </StackPanel>
                </Border>
              </Grid>
            </Border>

            <Border Background="{StaticResource BrushBgPanel}" CornerRadius="8"
                    BorderBrush="{StaticResource BrushBorder}" BorderThickness="1" MinHeight="160">
              <Grid RowDefinitions="Auto,*">
                <Border Grid.Row="0" Background="{StaticResource BrushPrimary}" Padding="8,6">
                  <TextBlock Text="试验信息过程" Foreground="White" FontSize="12" FontWeight="SemiBold"/>
                </Border>
                <Border Grid.Row="1" Background="{StaticResource BrushBgWindow}" MinHeight="120">
                  <ScrollViewer HorizontalScrollBarVisibility="Disabled" VerticalScrollBarVisibility="Auto">
                    <ItemsControl ItemsSource="{Binding TestProcessLogLines}">
                      <ItemsControl.ItemTemplate>
                        <DataTemplate>
                          <TextBlock Text="{Binding}" FontFamily="{StaticResource MonoFont}" FontSize="11"
                                     Foreground="White" Margin="8,4,8,0" TextWrapping="Wrap"/>
                        </DataTemplate>
                      </ItemsControl.ItemTemplate>
                    </ItemsControl>
                  </ScrollViewer>
                </Border>
              </Grid>
            </Border>

            <Border Background="{StaticResource BrushBgPanel}" CornerRadius="8"
                    BorderBrush="{StaticResource BrushBorder}" BorderThickness="1" Padding="8,6">
              <TextBlock Text="{Binding VoiceBroadcastText}" FontSize="11" TextWrapping="Wrap"
                         Foreground="{StaticResource BrushTextPrimary}"/>
            </Border>

            <Border Background="{StaticResource BrushBgPanel}" CornerRadius="8"
                    BorderBrush="{StaticResource BrushBorder}" BorderThickness="1" Padding="10,8">
              <StackPanel Orientation="Horizontal" Spacing="6" HorizontalAlignment="Center">
                <TextBlock Text="{Binding RemainSeconds}" FontSize="28" FontWeight="Bold"
                           FontFamily="{StaticResource MonoFont}" Foreground="{StaticResource BrushPrimary}"/>
                <TextBlock Text="S" FontSize="18" FontWeight="Bold" VerticalAlignment="Bottom" Margin="0,0,0,4"
                           Foreground="{StaticResource BrushTextSecondary}"/>
              </StackPanel>
              <TextBlock Text="试验时间" FontSize="10" HorizontalAlignment="Center" Margin="0,4,0,0"
                         Foreground="{StaticResource BrushTextTertiary}"/>
            </Border>

            <Border Background="#FF2A2F37" CornerRadius="8" Padding="8,6"
                    BorderBrush="{StaticResource BrushBorder}" BorderThickness="1">
              <StackPanel Spacing="4">
                <TextBlock FontSize="10" Foreground="{StaticResource BrushTextTertiary}"
                           Text="{Binding LiveMetricsLine}"/>
                <TextBlock Text="{Binding StatusText}" FontSize="11" Foreground="{StaticResource BrushSuccess}"/>
              </StackPanel>
            </Border>

            <Grid ColumnDefinitions="*,*" Margin="0,4,0,0">
              <Button Grid.Column="0" Classes="primary" Content="开始试验" Margin="0,0,4,0" MinHeight="44"
                      Command="{Binding StartTestCommand}"/>
              <Button Grid.Column="1" Classes="danger" Content="结束试验" Margin="4,0,0,0" MinHeight="44"
                      Command="{Binding StopTestCommand}"/>
            </Grid>

            <StackPanel Orientation="Vertical" Spacing="6" Margin="0,4,0,0">
              <StackPanel Orientation="Horizontal" Spacing="6" IsVisible="{Binding IsOnlineMode}">
                <Button Content="任务下载" Click="OnTaskDownloadClick" Padding="8,4" FontSize="11"/>
                <Button Content="任务上传" Click="OnTaskUploadClick" Padding="8,4" FontSize="11"/>
              </StackPanel>
              <Button Classes="info" Content="重新导入 Excel" Click="OnReimportExcelClick" FontSize="11"
                      IsVisible="{Binding IsExcelImportMode}" HorizontalAlignment="Stretch"/>
              <StackPanel Orientation="Horizontal" Spacing="6" IsVisible="{Binding IsOfflineMode}">
                <Button Content="新增型号" Click="OnNewProductClick" Padding="8,4" FontSize="11"/>
                <Button Content="选择型号" Click="OnSelectProductClick" Padding="8,4" FontSize="11"/>
              </StackPanel>
            </StackPanel>
          </StackPanel>
        </ScrollViewer>

        <Grid Grid.Column="1" Margin="0,0,10,0" RowDefinitions="Auto,Auto,Auto,*" MinHeight="0">
          <StackPanel Grid.Row="0" Orientation="Horizontal" Spacing="4" Margin="0,0,0,8">
            <Border Background="{StaticResource BrushPrimary}" CornerRadius="6" Padding="14,6">
              <TextBlock Text="工艺界面" Foreground="White" FontSize="12" FontWeight="SemiBold"/>
            </Border>
            <Border Background="{StaticResource BrushBgInput}" CornerRadius="6" Padding="14,6"
                    BorderBrush="{StaticResource BrushBorder}" BorderThickness="1">
              <TextBlock Text="摄像头一" Foreground="{StaticResource BrushTextSecondary}" FontSize="12"/>
            </Border>
          </StackPanel>

          <UniformGrid Grid.Row="1" Columns="5" Rows="1" Margin="0,0,0,8">
            <Border Background="#FF2A2F37" BorderBrush="#FF5B6270" BorderThickness="1" CornerRadius="6" Padding="6,4" Margin="2,0">
              <StackPanel>
                <TextBlock Text="{Binding HighVoltageOutput, StringFormat={}{0:F1}}" FontSize="15" FontWeight="Bold"
                           HorizontalAlignment="Center" Foreground="{StaticResource BrushTextPrimary}"/>
                <TextBlock Text="高压输出检测(V)" FontSize="9" HorizontalAlignment="Center"
                           Foreground="{StaticResource BrushTextTertiary}" Margin="0,2,0,0"/>
              </StackPanel>
            </Border>
            <Border Background="#FF2A2F37" BorderBrush="#FF5B6270" BorderThickness="1" CornerRadius="6" Padding="6,4" Margin="2,0">
              <StackPanel>
                <TextBlock Text="{Binding CurrentMa, StringFormat={}{0:F1}}" FontSize="15" FontWeight="Bold"
                           HorizontalAlignment="Center" Foreground="{StaticResource BrushTextPrimary}"/>
                <TextBlock Text="泄漏电流检测(mA)" FontSize="9" HorizontalAlignment="Center"
                           Foreground="{StaticResource BrushTextTertiary}" Margin="0,2,0,0"/>
              </StackPanel>
            </Border>
            <Border Background="#FF2A2F37" BorderBrush="#FF5B6270" BorderThickness="1" CornerRadius="6" Padding="6,4" Margin="2,0">
              <StackPanel>
                <TextBlock Text="{Binding VoltageKv, StringFormat={}{0:F1}}" FontSize="15" FontWeight="Bold"
                           HorizontalAlignment="Center" Foreground="{StaticResource BrushTextPrimary}"/>
                <TextBlock Text="厂房电压检测(V)" FontSize="9" HorizontalAlignment="Center"
                           Foreground="{StaticResource BrushTextTertiary}" Margin="0,2,0,0"/>
              </StackPanel>
            </Border>
            <Border Background="#FF2A2F37" BorderBrush="#FF5B6270" BorderThickness="1" CornerRadius="6" Padding="6,4" Margin="2,0">
              <StackPanel>
                <TextBlock Text="{Binding EnvTempC, StringFormat={}{0:F1}}" FontSize="15" FontWeight="Bold"
                           HorizontalAlignment="Center" Foreground="{StaticResource BrushTextPrimary}"/>
                <TextBlock Text="环境温度(℃)" FontSize="9" HorizontalAlignment="Center"
                           Foreground="{StaticResource BrushTextTertiary}" Margin="0,2,0,0"/>
              </StackPanel>
            </Border>
            <Border Background="#FF2A2F37" BorderBrush="#FF5B6270" BorderThickness="1" CornerRadius="6" Padding="6,4" Margin="2,0">
              <StackPanel>
                <TextBlock Text="{Binding EnvHumidity, StringFormat={}{0:F1}}" FontSize="15" FontWeight="Bold"
                           HorizontalAlignment="Center" Foreground="{StaticResource BrushTextPrimary}"/>
                <TextBlock Text="环境湿度(%)" FontSize="9" HorizontalAlignment="Center"
                           Foreground="{StaticResource BrushTextTertiary}" Margin="0,2,0,0"/>
              </StackPanel>
            </Border>
          </UniformGrid>

          <Border Grid.Row="2" Margin="0,0,0,8">
__SIGNALS__
          </Border>

          <Border Grid.Row="3" Background="{StaticResource BrushBgPanel}" CornerRadius="10" Padding="10,8"
                  BorderBrush="{StaticResource BrushBorder}" BorderThickness="1" MinHeight="200">
            <Grid RowDefinitions="Auto,*">
              <Grid Grid.Row="0" ColumnDefinitions="*,Auto" Margin="0,0,0,6">
                <TextBlock Text="耐压曲线" FontSize="14" FontWeight="Medium"
                           Foreground="{StaticResource BrushTextPrimary}"/>
                <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="14">
                  <StackPanel Orientation="Horizontal" Spacing="6">
                    <Border Width="18" Height="3" CornerRadius="2" Background="{StaticResource BrushPrimary}"/>
                    <TextBlock Text="电压 (kV)" FontSize="12" Foreground="{StaticResource BrushTextSecondary}"/>
                  </StackPanel>
                  <StackPanel Orientation="Horizontal" Spacing="6">
                    <Border Width="18" Height="3" CornerRadius="2" Background="{StaticResource BrushInfo}"/>
                    <TextBlock Text="泄漏电流 (mA)" FontSize="12" Foreground="{StaticResource BrushTextSecondary}"/>
                  </StackPanel>
                </StackPanel>
              </Grid>
__CHART__
            </Grid>
          </Border>
        </Grid>

        <StackPanel Grid.Column="2" Spacing="8">
          <Border Background="{StaticResource BrushBgPanel}" CornerRadius="8"
                  BorderBrush="{StaticResource BrushBorder}" BorderThickness="1" ClipToBounds="True">
            <StackPanel Spacing="0">
              <Border Background="{StaticResource BrushPrimary}" Padding="8,6">
                <TextBlock Text="模拟量输出" Foreground="White" FontSize="12" FontWeight="SemiBold" HorizontalAlignment="Center"/>
              </Border>
              <StackPanel Margin="8,8,8,4" Spacing="8">
                <StackPanel Spacing="2">
                  <TextBlock Text="试验时间设定(S)" FontSize="10" Foreground="{StaticResource BrushTextTertiary}"/>
                  <NumericUpDown Classes="param-input" Value="{Binding ManualDurationSeconds}"
                                 Minimum="1" Maximum="3600" Increment="1" FormatString="F0" MinHeight="28" FontSize="16"/>
                </StackPanel>
                <StackPanel Spacing="2">
                  <TextBlock Text="保护电流(mA)" FontSize="10" Foreground="{StaticResource BrushTextTertiary}"/>
                  <NumericUpDown Classes="param-input" Value="{Binding ManualProtectMa}"
                                 Minimum="0" Maximum="2000" Increment="10" FormatString="F0" MinHeight="28" FontSize="16"/>
                </StackPanel>
                <StackPanel Spacing="2">
                  <TextBlock Text="自定义电压设定(V)" FontSize="10" Foreground="{StaticResource BrushTextTertiary}"/>
                  <NumericUpDown Classes="param-input" Value="{Binding ManualSetVoltageKv}"
                                 Minimum="0" Maximum="15" Increment="0.1" FormatString="F2" MinHeight="28" FontSize="16"/>
                </StackPanel>
              </StackPanel>
            </StackPanel>
          </Border>

          <Border Background="{StaticResource BrushBgInput}" CornerRadius="8"
                  BorderBrush="{StaticResource BrushBorder}" BorderThickness="1" ClipToBounds="True" MinHeight="220">
            <Grid RowDefinitions="Auto,*">
              <Border Grid.Row="0" Background="#FF2F343D" Padding="10,8"
                      BorderBrush="{StaticResource BrushBorder}" BorderThickness="0,0,0,1">
                <TextBlock Text="任务管理" FontSize="13" FontWeight="Medium"
                           Foreground="{StaticResource BrushTextPrimary}" HorizontalAlignment="Center"/>
              </Border>
              <Grid Grid.Row="1" Margin="8,8,8,8" RowDefinitions="*,*,*,*" MinHeight="0">
                <Button Grid.Row="0" Classes="task-mgmt-btn" Margin="0,0,0,6" Click="OnTaskDownloadClick">
                  <StackPanel Orientation="Horizontal" Spacing="10">
                    <TextBlock Text="📥" FontSize="16" VerticalAlignment="Center"/>
                    <TextBlock Text="任务下载" FontSize="13" VerticalAlignment="Center"/>
                  </StackPanel>
                </Button>
                <Button Grid.Row="1" Classes="task-mgmt-btn" Margin="0,0,0,6" Click="OnTaskViewClick">
                  <StackPanel Orientation="Horizontal" Spacing="10">
                    <TextBlock Text="📋" FontSize="16" VerticalAlignment="Center"/>
                    <TextBlock Text="任务查看" FontSize="13" VerticalAlignment="Center"/>
                  </StackPanel>
                </Button>
                <Button Grid.Row="2" Classes="task-mgmt-btn" Margin="0,0,0,6" Click="OnTaskUploadClick">
                  <StackPanel Orientation="Horizontal" Spacing="10">
                    <TextBlock Text="📤" FontSize="16" VerticalAlignment="Center"/>
                    <TextBlock Text="任务上传" FontSize="13" VerticalAlignment="Center"/>
                  </StackPanel>
                </Button>
                <Button Grid.Row="3" Classes="task-mgmt-btn" Click="OnInsulationTestClick">
                  <StackPanel Orientation="Horizontal" Spacing="10">
                    <TextBlock Text="⚡" FontSize="16" VerticalAlignment="Center"/>
                    <TextBlock Text="绝缘测试" FontSize="13" VerticalAlignment="Center"/>
                  </StackPanel>
                </Button>
              </Grid>
            </Grid>
          </Border>

          <Button Classes="primary" Content="🖨  打印报表" HorizontalAlignment="Stretch" MinHeight="40"/>
        </StackPanel>
      </Grid>
""".replace("__SIGNALS__", signals_block).replace("__CHART__", chart_block)

text = text[:i_start] + NEW + "\n      </Grid>\n" + text[i_content_close:]
path.write_text(text, encoding="utf-8")
print("patched", path)
